using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Threading;
using LT.App.Helpers;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.MarketData;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;
using LT.Contracts.Interfaces.IManagers;

namespace LT.App.Widgets.Overview;

/// <summary>
/// The instrument at a glance: what it is (name, type, listing, identifiers), how it is
/// classified, how and when it trades (market state now, today's hours, tick, routing) and, for
/// US stocks, shares outstanding from the SEC with the market cap at the live price. Fields IB
/// left blank are left out; what only the details hold appears once they are loaded.
/// </summary>
public class BasicInfoTabViewModel : OverviewTabViewModel
{
	/// <summary>Market cap's meaning, here and on the Financials tab.</summary>
	public const string MarketCapDescription =
		"What the market values the whole company at: shares outstanding times the share price.";

	private readonly ISharesOutstandingManager _sharesOutstandingManager;
	private readonly TimeProvider _timeProvider;
	private readonly DispatcherTimer _stateTimer;
	private ContractDto? _contract;
	private ContractDetailsDto? _details;
	private QuoteDto? _quote;
	private long? _shares;
	private bool _sharesLoading;
	private OverviewField? _marketState;
	private OverviewField? _sharesField;
	private OverviewField? _marketCapField;

	public BasicInfoTabViewModel(
		ISharesOutstandingManager sharesOutstandingManager,
		TimeProvider timeProvider
	)
	{
		_sharesOutstandingManager = sharesOutstandingManager;
		_timeProvider = timeProvider;
		// "Open · closes 16:00" turns into "After hours" without any new data
		_stateTimer = new DispatcherTimer(
			OverviewConstants.MarketStateRefreshInterval,
			DispatcherPriority.Background,
			(_, _) => UpdateMarketState()
		);
		_stateTimer.Start();
	}

	public override OverviewTab Kind => OverviewTab.BasicInfo;

	public override string Header => "Basic info";

	public ObservableCollection<OverviewSection> Sections { get; } = [];

	public override void Show(
		ContractDto? contract,
		ContractDetailsDto? details,
		CancellationToken load
	)
	{
		var changed = contract?.ConId != _contract?.ConId;
		_contract = contract;
		_details = details;
		if (changed)
		{
			_shares = null;
			_sharesLoading = false;
			if (contract != null && MarketCapHelpers.IsEligible(contract))
				_ = LoadSharesAsync(contract, load);
		}
		Rebuild();
	}

	public override void OnQuote(QuoteDto? quote)
	{
		_quote = quote;
		UpdateShares();
	}

	public override void Dispose()
	{
		_stateTimer.Stop();
		base.Dispose();
	}

	private async Task LoadSharesAsync(ContractDto contract, CancellationToken load)
	{
		_sharesLoading = true;
		try
		{
			// Never throws but on cancellation; null: nothing usable at the SEC
			var shares = await _sharesOutstandingManager.GetSharesOutstandingAsync(contract, load);
			if (load.IsCancellationRequested || contract.ConId != _contract?.ConId)
				return;
			_shares = shares;
		}
		catch (OperationCanceledException)
		{
			return;
		}
		_sharesLoading = false;
		UpdateShares();
	}

	private void Rebuild()
	{
		Sections.Clear();
		_marketState = _sharesField = _marketCapField = null;
		if (_contract is not { } contract)
			return;

		AddSection("Instrument", GetInstrumentFields(contract, _details));
		if (_details is { } details)
		{
			AddSection(
				"Classification",
				[
					Field(
						"Industry",
						details.Industry,
						description: "The broad sector IB classifies the company under."
					),
					Field(
						"Category",
						details.Category,
						description: "The industry group within the sector."
					),
					Field(
						"Subcategory",
						details.Subcategory,
						description: "The narrowest level of IB's industry classification, within the category."
					),
				]
			);
			AddSection("Trading", GetTradingFields(contract, details));
		}
		if (MarketCapHelpers.IsEligible(contract))
		{
			_sharesField = new OverviewField(
				"Shares outstanding",
				string.Empty,
				description: "Every share of the company held by its shareholders, insiders and institutions included."
			);
			_marketCapField = new OverviewField(
				"Market cap",
				string.Empty,
				description: MarketCapDescription
			);
			AddSection("Shares", [_sharesField, _marketCapField]);
			UpdateShares();
		}
		UpdateMarketState();
	}

	private static List<OverviewField?> GetInstrumentFields(
		ContractDto contract,
		ContractDetailsDto? details
	)
	{
		var fields = new List<OverviewField?>
		{
			Field(
				"Name",
				details?.LongName,
				description: "The instrument's full name as IB lists it: the company, fund or index."
			),
			Field(
				"Symbol",
				contract.Symbol,
				description: "The ticker IB knows the instrument by, the one typed to find it."
			),
			contract.LocalSymbol != contract.Symbol
				? Field(
					"Local symbol",
					contract.LocalSymbol,
					description: "The symbol on the exchange itself where it differs from the ticker, e.g. an option's OCC symbol or a future's month code."
				)
				: null,
			Field(
				"Type",
				OverviewHelpers.GetSecurityTypeName(contract.SecurityType),
				description: "IB's security type: what kind of instrument this is. ETFs are stocks to IB."
			),
			Field(
				"Exchange",
				ContractHelpers.GetListingExchange(contract),
				description: "The primary exchange the instrument is listed on. Orders can still be routed to other venues (see Routes to)."
			),
			Field(
				"Currency",
				contract.Currency,
				description: "The currency the instrument is quoted and traded in."
			),
			Field(
				"Contract ID",
				contract.ConId > 0 ? contract.ConId.ToString(CultureInfo.InvariantCulture) : null,
				description: "IB's unique number for this exact contract (conId): it identifies it unambiguously, unlike a ticker."
			),
		};
		foreach (var (type, id) in (details?.SecurityIds ?? []).OrderBy(x => x.Key))
			fields.Add(Field(type, id, description: OverviewHelpers.DescribeSecurityId(type)));
		var market = details?.MarketName is { Length: > 0 } name ? name : contract.TradingClass;
		if (market != contract.Symbol)
			fields.Add(
				Field(
					"Market",
					market,
					description: "IB's market name (trading class) the contract belongs to, e.g. NMS for Nasdaq-listed stocks or an option's class."
				)
			);
		if (contract.LastTradeDateOrContractMonth.Length > 0)
			fields.Add(
				Field(
					"Expiry",
					contract.LastTradeDateOrContractMonth,
					description: "The last trading day (or contract month) of the derivative; it can't be traded after it."
				)
			);
		if (contract.Strike > 0)
			fields.Add(
				Field(
					"Strike",
					$"{contract.Strike.ToString("0.####", CultureInfo.CurrentCulture)} {contract.Right}",
					description: "The price the option can be exercised at; C is a call (the right to buy), P a put (the right to sell)."
				)
			);
		if (ContractHelpers.GetMultiplier(contract) is { } multiplier)
			fields.Add(
				Field(
					"Multiplier",
					multiplier.ToString("0.####", CultureInfo.CurrentCulture),
					description: "Units one contract stands for: a price move of 1 changes one contract's value by this much (100 for US stock options)."
				)
			);
		return fields;
	}

	private List<OverviewField?> GetTradingFields(ContractDto contract, ContractDetailsDto details)
	{
		var now = _timeProvider.GetUtcNow();
		_marketState = new OverviewField(
			"Market",
			string.Empty,
			"In the exchange's time zone",
			"Whether the market is open right now (regular session, pre-market, after hours or closed) and when that changes."
		);
		var exchanges = OverviewHelpers.GetValidExchanges(details.ValidExchanges);
		return
		[
			_marketState,
			Field(
				"Regular hours",
				OverviewHelpers.DescribeToday(details.LiquidHours, details.TimeZoneId, now),
				"Today's regular session, exchange time",
				"The regular (liquid) trading session, when most of the volume trades and the official open and close are set."
			),
			Field(
				"Extended hours",
				OverviewHelpers.DescribeToday(details.TradingHours, details.TimeZoneId, now),
				"Every session today, pre- and post-market included, exchange time",
				"IB's full trading hours. Outside the regular hours an order only fills if it is allowed to trade outside regular trading hours (RTH)."
			),
			Field(
				"Time zone",
				details.TimeZoneId,
				description: "The exchange's time zone; the hours in this tab are in it."
			),
			Field(
				"Min tick",
				OverviewHelpers.FormatMinTick(details.MinTick),
				description: "The smallest price increment the instrument is quoted and ordered in: a limit price must be a multiple of it."
			),
			Field(
				"Contract month",
				details.ContractMonth,
				description: "The month the future or option contract is for (YYYYMM)."
			),
			exchanges.Count > 0
				? Field(
					"Routes to",
					exchanges.Count == 1 ? exchanges[0] : $"{exchanges.Count} exchanges",
					string.Join(", ", exchanges),
					"The exchanges IB can send orders for this contract to; SMART routing picks among them for the best price."
				)
				: null,
			details.UnderlyingConId > 0 && details.UnderlyingConId != contract.ConId
				? Field(
					"Underlying ID",
					details.UnderlyingConId.ToString(CultureInfo.InvariantCulture),
					description: "IB's contract ID (conId) of the instrument this derivative is based on."
				)
				: null,
		];
	}

	private void UpdateMarketState()
	{
		if (_marketState == null || _details is not { } details)
			return;
		_marketState.Value =
			OverviewHelpers.DescribeMarketState(
				details.TradingHours,
				details.LiquidHours,
				details.TimeZoneId,
				_timeProvider.GetUtcNow()
			) ?? FormatHelpers.Empty;
	}

	private void UpdateShares()
	{
		if (_sharesField == null || _marketCapField == null)
			return;
		if (_sharesLoading)
		{
			_sharesField.Value = _marketCapField.Value = "Loading…";
			_sharesField.ToolTip = "Looking the count up in the company's SEC filings";
			return;
		}
		if (_shares is not { } shares)
		{
			_sharesField.Value = _marketCapField.Value = FormatHelpers.Empty;
			_sharesField.ToolTip = _marketCapField.ToolTip =
				"The SEC has no usable share count for this ticker (or can't be reached)";
			return;
		}
		_sharesField.Value = FormatHelpers.WholeNumber(shares);
		_sharesField.ToolTip = "From the company's latest SEC filings";
		var marketCap = MarketCapHelpers.Compute(shares, _quote);
		_marketCapField.Value =
			marketCap != null ? FormatHelpers.MarketCap(marketCap) : "Waiting for a price…";
		_marketCapField.ToolTip = "Shares outstanding × the last price";
	}

	private void AddSection(string title, IEnumerable<OverviewField?> fields)
	{
		var shown = fields.OfType<OverviewField>().ToList();
		if (shown.Count > 0)
			Sections.Add(new OverviewSection(title, shown));
	}

	// Blank values are left out
	private static OverviewField? Field(
		string label,
		string? value,
		string? toolTip = null,
		string? description = null
	) =>
		string.IsNullOrWhiteSpace(value)
			? null
			: new OverviewField(label, value.Trim(), toolTip, description);
}
