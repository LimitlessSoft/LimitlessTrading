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
					Field("Industry", details.Industry),
					Field("Category", details.Category),
					Field("Subcategory", details.Subcategory),
				]
			);
			AddSection("Trading", GetTradingFields(contract, details));
		}
		if (MarketCapHelpers.IsEligible(contract))
		{
			_sharesField = new OverviewField("Shares outstanding", string.Empty);
			_marketCapField = new OverviewField("Market cap", string.Empty);
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
			Field("Name", details?.LongName),
			Field("Symbol", contract.Symbol),
			contract.LocalSymbol != contract.Symbol
				? Field("Local symbol", contract.LocalSymbol)
				: null,
			Field("Type", OverviewHelpers.GetSecurityTypeName(contract.SecurityType)),
			Field("Exchange", ContractHelpers.GetListingExchange(contract)),
			Field("Currency", contract.Currency),
			Field(
				"Contract ID",
				contract.ConId > 0 ? contract.ConId.ToString(CultureInfo.InvariantCulture) : null
			),
		};
		foreach (var (type, id) in (details?.SecurityIds ?? []).OrderBy(x => x.Key))
			fields.Add(Field(type, id));
		var market = details?.MarketName is { Length: > 0 } name ? name : contract.TradingClass;
		if (market != contract.Symbol)
			fields.Add(Field("Market", market));
		if (contract.LastTradeDateOrContractMonth.Length > 0)
			fields.Add(Field("Expiry", contract.LastTradeDateOrContractMonth));
		if (contract.Strike > 0)
			fields.Add(
				Field(
					"Strike",
					$"{contract.Strike.ToString("0.####", CultureInfo.CurrentCulture)} {contract.Right}"
				)
			);
		if (ContractHelpers.GetMultiplier(contract) is { } multiplier)
			fields.Add(
				Field("Multiplier", multiplier.ToString("0.####", CultureInfo.CurrentCulture))
			);
		return fields;
	}

	private List<OverviewField?> GetTradingFields(ContractDto contract, ContractDetailsDto details)
	{
		var now = _timeProvider.GetUtcNow();
		_marketState = new OverviewField("Market", string.Empty, "In the exchange's time zone");
		var exchanges = OverviewHelpers.GetValidExchanges(details.ValidExchanges);
		return
		[
			_marketState,
			Field(
				"Regular hours",
				OverviewHelpers.DescribeToday(details.LiquidHours, details.TimeZoneId, now),
				"Today's regular session, exchange time"
			),
			Field(
				"Extended hours",
				OverviewHelpers.DescribeToday(details.TradingHours, details.TimeZoneId, now),
				"Every session today, pre- and post-market included, exchange time"
			),
			Field("Time zone", details.TimeZoneId),
			Field("Min tick", OverviewHelpers.FormatMinTick(details.MinTick)),
			Field("Contract month", details.ContractMonth),
			exchanges.Count > 0
				? Field(
					"Routes to",
					exchanges.Count == 1 ? exchanges[0] : $"{exchanges.Count} exchanges",
					string.Join(", ", exchanges)
				)
				: null,
			details.UnderlyingConId > 0 && details.UnderlyingConId != contract.ConId
				? Field(
					"Underlying ID",
					details.UnderlyingConId.ToString(CultureInfo.InvariantCulture)
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
	private static OverviewField? Field(string label, string? value, string? toolTip = null) =>
		string.IsNullOrWhiteSpace(value) ? null : new OverviewField(label, value.Trim(), toolTip);
}
