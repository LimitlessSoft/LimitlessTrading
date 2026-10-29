using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using LT.App.Helpers;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.Fundamentals;
using LT.Contracts.Dtos.MarketData;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;
using LT.Contracts.Interfaces.IManagers;

namespace LT.App.Widgets.Overview;

/// <summary>
/// The company's income statement, balance sheet, cash flow and ratios per fiscal year or quarter,
/// from its SEC 10-K / 10-Q filings (US stocks only; IB's own fundamentals need an entitlement,
/// error 10358), with valuation multiples at the live price over the trailing twelve months.
/// Loads the first time the tab is shown for a contract, not before: the SEC's facts are
/// several MB.
/// </summary>
public partial class FinancialsTabViewModel : OverviewTabViewModel
{
	private const string DerivedToolTip =
		"Not reported for the quarter: the year to date less the previous year to date (Q4: the 10-K less Q1–Q3)";

	private readonly IFinancialsManager _financialsManager;
	private readonly ISharesOutstandingManager _sharesOutstandingManager;
	private readonly OverviewField _marketCap = new("Market cap", FormatHelpers.Empty);
	private readonly OverviewField _priceToEarnings = new(
		"P/E (TTM)",
		FormatHelpers.Empty,
		"Market cap / net income of the last four quarters"
	);
	private readonly OverviewField _priceToSales = new(
		"P/S (TTM)",
		FormatHelpers.Empty,
		"Market cap / revenue of the last four quarters"
	);
	private readonly OverviewField _priceToBook = new(
		"P/B",
		FormatHelpers.Empty,
		"Market cap / shareholders' equity of the latest quarter"
	);
	private readonly OverviewField _priceToFreeCashFlow = new(
		"P/FCF (TTM)",
		FormatHelpers.Empty,
		"Market cap / free cash flow of the last four quarters"
	);
	private readonly OverviewField _revenueTtm = new(
		"Revenue (TTM)",
		FormatHelpers.Empty,
		"The last four quarters"
	);
	private readonly OverviewField _netIncomeTtm = new(
		"Net income (TTM)",
		FormatHelpers.Empty,
		"The last four quarters"
	);
	private ContractDto? _contract;
	private CancellationToken _load;
	private bool _selected;
	private bool _requested;
	private FinancialsDto? _financials;
	private long? _shares;
	private QuoteDto? _quote;

	public FinancialsTabViewModel(
		IFinancialsManager financialsManager,
		ISharesOutstandingManager sharesOutstandingManager
	)
	{
		_financialsManager = financialsManager;
		_sharesOutstandingManager = sharesOutstandingManager;
		Valuation =
		[
			_marketCap,
			_priceToEarnings,
			_priceToSales,
			_priceToBook,
			_priceToFreeCashFlow,
			_revenueTtm,
			_netIncomeTtm,
		];
	}

	public override OverviewTab Kind => OverviewTab.Financials;

	public override string Header => "Financials";

	/// <summary>The periods shown, newest first: one column each.</summary>
	public ObservableCollection<FinancialColumn> Columns { get; } = [];

	/// <summary>Income statement, balance sheet, cash flow, ratios: lines with a value in some period.</summary>
	public ObservableCollection<FinancialSection> Sections { get; } = [];

	/// <summary>Multiples at the live price and the trailing twelve months.</summary>
	public IReadOnlyList<OverviewField> Valuation { get; }

	/// <summary>Quarters shown instead of fiscal years.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(IsAnnual))]
	public partial bool IsQuarterly { get; set; }

	/// <summary>The other side of <see cref="IsQuarterly"/>, for the Annual toggle.</summary>
	public bool IsAnnual
	{
		get => !IsQuarterly;
		set => IsQuarterly = !value;
	}

	/// <summary>Statements are shown (else <see cref="StatusText"/> says why not).</summary>
	[ObservableProperty]
	public partial bool HasStatements { get; set; }

	/// <summary>Loading, or why there are no statements (not a US stock, ETF, SEC unreachable).</summary>
	[ObservableProperty]
	public partial string? StatusText { get; set; }

	[ObservableProperty]
	public partial bool IsLoading { get; set; }

	/// <summary>Where the numbers come from: "Apple Inc. · SEC 10-K / 10-Q filings · USD".</summary>
	[ObservableProperty]
	public partial string? SourceText { get; set; }

	public override void Show(
		ContractDto? contract,
		ContractDetailsDto? details,
		CancellationToken load
	)
	{
		if (contract?.ConId == _contract?.ConId && contract?.Symbol == _contract?.Symbol)
			return;
		_contract = contract;
		_load = load;
		_requested = false;
		_financials = null;
		_shares = null;
		StatusText = SourceText = null;
		IsLoading = HasStatements = false;
		Columns.Clear();
		Sections.Clear();
		UpdateValuation();
		LoadIfShown();
	}

	public override void OnSelected(bool selected)
	{
		_selected = selected;
		LoadIfShown();
	}

	public override void OnQuote(QuoteDto? quote)
	{
		_quote = quote;
		UpdateValuation();
	}

	partial void OnIsQuarterlyChanged(bool value) => Rebuild();

	private void LoadIfShown()
	{
		if (!_selected || _requested || _contract is not { } contract)
			return;
		_requested = true;
		_ = LoadAsync(contract, _load);
	}

	// The answers are posted to the UI thread in order: the statements, then the share count
	private async Task LoadAsync(ContractDto contract, CancellationToken load)
	{
		IsLoading = true;
		StatusText = "Loading the company's SEC filings…";
		var shares = _sharesOutstandingManager.GetSharesOutstandingAsync(contract, load);
		try
		{
			// Never throws but on cancellation
			var financials = await _financialsManager.GetFinancialsAsync(contract, load);
			Dispatcher.UIThread.Post(() =>
			{
				if (IsCurrent(contract, load))
					ShowFinancials(contract, financials);
			});
			var count = await shares;
			Dispatcher.UIThread.Post(() =>
			{
				if (!IsCurrent(contract, load))
					return;
				_shares = count;
				UpdateValuation();
			});
		}
		catch (OperationCanceledException) { }
	}

	private bool IsCurrent(ContractDto contract, CancellationToken load) =>
		!load.IsCancellationRequested && contract.ConId == _contract?.ConId;

	private void ShowFinancials(ContractDto contract, FinancialsDto financials)
	{
		_financials = financials;
		Rebuild();
		UpdateValuation();
		IsLoading = false;
		StatusText = GetStatusText(financials, contract);
		SourceText =
			financials.Status == FinancialsStatus.Available
				? string.Join(
					" · ",
					new[] { financials.EntityName, "SEC 10-K / 10-Q filings", "USD" }.Where(x =>
						!string.IsNullOrWhiteSpace(x)
					)
				)
				: null;
		HasStatements = financials.Status == FinancialsStatus.Available;
	}

	private static string? GetStatusText(FinancialsDto financials, ContractDto contract) =>
		financials.Status switch
		{
			FinancialsStatus.Available => null,
			FinancialsStatus.NotEligible =>
				$"No financial statements for {contract.Symbol}: they come from SEC filings (10-K / 10-Q), which only US stocks traded in USD have.",
			FinancialsStatus.NotFiled =>
				$"The SEC has no 10-K / 10-Q statements for {contract.Symbol}. ETFs and funds (like SPY) don't file them, and foreign companies file 20-F / 40-F reports (IFRS), which aren't read here.",
			_ =>
				"The SEC couldn't be reached, so there are no statements to show (see Messages). They are asked for again when this ticker is shown next.",
		};

	private void Rebuild()
	{
		Columns.Clear();
		Sections.Clear();
		if (_financials is not { Status: FinancialsStatus.Available } financials)
			return;
		var periods = IsQuarterly ? financials.Quarterly : financials.Annual;
		foreach (var period in periods)
			Columns.Add(
				new FinancialColumn(
					GetPeriodName(period),
					FormatDate(period.End),
					$"{FormatDate(period.Start)} – {FormatDate(period.End)}"
				)
			);

		AddSection("Income statement", FinancialStatement.IncomeStatement, periods);
		AddSection("Balance sheet", FinancialStatement.BalanceSheet, periods);
		AddSection("Cash flow", FinancialStatement.CashFlow, periods);
		var ratios = new List<FinancialRow>();
		AddRatio(ratios, periods, FinancialRatio.GrossMargin, "Gross margin", true);
		AddRatio(ratios, periods, FinancialRatio.OperatingMargin, "Operating margin", true);
		AddRatio(ratios, periods, FinancialRatio.NetMargin, "Net margin", true);
		AddRatio(ratios, periods, FinancialRatio.FreeCashFlowMargin, "FCF margin", true);
		AddRatio(
			ratios,
			periods,
			FinancialRatio.ReturnOnEquity,
			"Return on equity",
			true,
			"Net income / shareholders' equity at the year's end (fiscal years only)"
		);
		AddRatio(
			ratios,
			periods,
			FinancialRatio.CurrentRatio,
			"Current ratio",
			false,
			"Current assets / current liabilities"
		);
		AddRatio(
			ratios,
			periods,
			FinancialRatio.DebtToEquity,
			"Debt / equity",
			false,
			"(Current + long-term debt) / shareholders' equity"
		);
		if (ratios.Count > 0)
			Sections.Add(new FinancialSection("Ratios", ratios));
	}

	private void AddSection(
		string title,
		FinancialStatement statement,
		IReadOnlyList<FinancialPeriodDto> periods
	)
	{
		var rows = new List<FinancialRow>();
		foreach (var definition in FinancialsConstants.Items)
		{
			if (
				definition.Statement != statement
				|| definition.Label == null
				|| periods.All(x => x.Get(definition.Item) == null)
			)
				continue;
			rows.Add(
				new FinancialRow(
					definition.Label,
					definition.ToolTip,
					periods
						.Select(period =>
						{
							var value = period.Get(definition.Item);
							return new FinancialCell(
								Format(definition.Kind, value),
								period.Derived.Contains(definition.Item) ? DerivedToolTip : null,
								value < 0
							);
						})
						.ToList()
				)
			);
		}
		if (rows.Count > 0)
			Sections.Add(new FinancialSection(title, rows));
	}

	private static void AddRatio(
		List<FinancialRow> rows,
		IReadOnlyList<FinancialPeriodDto> periods,
		FinancialRatio ratio,
		string label,
		bool percent,
		string? toolTip = null
	)
	{
		var values = periods.Select(x => FinancialsHelpers.GetRatio(ratio, x)).ToList();
		if (values.All(x => x == null))
			return;
		rows.Add(
			new FinancialRow(
				label,
				toolTip,
				values
					.Select(x => new FinancialCell(
						percent ? FormatHelpers.Percent(x)
							: x is { } v ? v.ToString("0.00", CultureInfo.CurrentCulture)
							: FormatHelpers.Empty,
						null,
						x < 0
					))
					.ToList()
			)
		);
	}

	private void UpdateValuation()
	{
		var ttm = _financials?.TrailingTwelveMonths;
		var marketCap = MarketCapHelpers.Compute(_shares, _quote);
		var equity = _financials?.Quarterly.FirstOrDefault()?.Get(FinancialItem.Equity);
		_marketCap.Value = FormatHelpers.MarketCap(marketCap);
		_marketCap.ToolTip = "SEC shares outstanding × the last price";
		_priceToEarnings.Value = FormatMultiple(marketCap, Get(ttm, FinancialItem.NetIncome));
		_priceToSales.Value = FormatMultiple(marketCap, Get(ttm, FinancialItem.Revenue));
		_priceToBook.Value = FormatMultiple(marketCap, equity);
		_priceToFreeCashFlow.Value = FormatMultiple(
			marketCap,
			Get(ttm, FinancialItem.FreeCashFlow)
		);
		_revenueTtm.Value = FormatHelpers.CompactAmount(Get(ttm, FinancialItem.Revenue));
		_netIncomeTtm.Value = FormatHelpers.CompactAmount(Get(ttm, FinancialItem.NetIncome));
	}

	private static decimal? Get(Dictionary<FinancialItem, decimal>? values, FinancialItem item) =>
		values != null && values.TryGetValue(item, out var value) ? value : null;

	// A loss or negative cash flow has no multiple
	private static string FormatMultiple(double? marketCap, decimal? amount) =>
		FinancialsHelpers.GetMultiple(marketCap, amount) is { } multiple
			? multiple.ToString("0.0", CultureInfo.CurrentCulture)
			: FormatHelpers.Empty;

	private static string Format(FinancialValueKind kind, decimal? value) =>
		kind == FinancialValueKind.PerShare
			? value is >= 1_000_000
				? FormatHelpers.CompactAmount(value)
				: FormatHelpers.Money(value)
			: FormatHelpers.CompactAmount(value);

	private static string GetPeriodName(FinancialPeriodDto period) =>
		period.FiscalQuarter is { } quarter
			? $"Q{quarter} FY{period.FiscalYear}"
			: $"FY{period.FiscalYear}";

	private static string FormatDate(DateOnly date) =>
		date.ToString("MMM d, yyyy", CultureInfo.CurrentCulture);
}

/// <summary>One period's column header: "Q3 FY2026" over "Jun 27, 2026".</summary>
public record FinancialColumn(string Name, string EndText, string ToolTip);

/// <summary>A statement ("Income statement" …) and its lines.</summary>
public record FinancialSection(string Title, IReadOnlyList<FinancialRow> Rows);

/// <summary>One line: its label and a cell per column.</summary>
public record FinancialRow(string Label, string? ToolTip, IReadOnlyList<FinancialCell> Cells);

/// <summary>One value; the tool tip says when it was worked out rather than reported.</summary>
public record FinancialCell(string Text, string? ToolTip, bool IsNegative)
{
	/// <summary>Worked out rather than reported (shown dimmed).</summary>
	public bool IsDerived => ToolTip != null;
}
