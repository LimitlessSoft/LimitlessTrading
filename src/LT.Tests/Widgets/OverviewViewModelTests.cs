using Avalonia.Threading;
using LT.App.Stores;
using LT.App.Widgets.Overview;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.MarketData;
using LT.Contracts.Dtos.Settings;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;
using LT.Contracts.Interfaces.IManagers;
using LT.Domain.Managers;
using LT.Tests.Fakes;

namespace LT.Tests.Widgets;

/// <summary>
/// The Overview widget: one contract from the link group, a typed ticker or a drop, saved with
/// the widget; its IB details handed to the Basic info tab with clear states while loading,
/// disconnected or not found; one quote line (with generic tick 165) only while shown.
/// </summary>
public class OverviewViewModelTests : IDisposable
{
	private static readonly ContractDto Spy = Stock(756733, "SPY", "ARCA");
	private static readonly ContractDto Aapl = Stock(265598, "AAPL", "NASDAQ");
	private static readonly ContractDto Sap = new()
	{
		ConId = 14204,
		Symbol = "SAP",
		SecurityType = "STK",
		Exchange = "SMART",
		PrimaryExchange = "IBIS",
		Currency = "EUR",
	};

	private readonly TestCache _cache = new();
	private readonly FakeIbkrApiClient _ibkr = new()
	{
		IsConnected = true,
		KeepMarketDataOpen = true,
		Ticks =
		[
			new MarketDataTickDto { Field = TickTypeHelpers.Last, Price = 600 },
			new MarketDataTickDto { Field = TickTypeHelpers.Close, Price = 590 },
			new MarketDataTickDto { Field = TickTypeHelpers.Week52Low, Price = 480 },
			new MarketDataTickDto { Field = TickTypeHelpers.Week52High, Price = 610 },
		],
	};
	private readonly FakeConnectionManager _connection = new();
	private readonly LinkGroupStore _links = new();
	private readonly StubSharesOutstanding _shares = new();
	private readonly MarketDataManager _marketData;
	private readonly Dictionary<int, ContractDetailsDto> _details = [];
	private readonly ContractManager _contracts;
	private OverviewViewModel? _overview;

	public OverviewViewModelTests()
	{
		_marketData = new MarketDataManager(_ibkr, TimeProvider.System);
		_contracts = new ContractManager(_ibkr, _cache.Settings, _cache.Manager);
		Detail(Spy, "SPDR S&P 500 ETF TRUST");
		Detail(Aapl, "APPLE INC", "Technology");
		Detail(Sap, "SAP SE");
		// A conId asks for its details; a symbol for its listings (ticker box)
		_ibkr.SearchBy = query =>
			query.ConId > 0
				? _details.TryGetValue(query.ConId, out var details)
					? [details]
					: []
				: query.SecurityType == "STK"
					? _details.Values.Where(x => x.Contract.Symbol == query.Symbol).ToList()
					: [];
	}

	[Fact]
	public void Without_a_contract_it_says_how_to_pick_one()
	{
		var overview = Create();

		Assert.False(overview.HasContract);
		Assert.Equal("Overview", overview.Title);
		Assert.False(string.IsNullOrEmpty(overview.InfoText));
		Assert.Empty(BasicInfo(overview).Sections);
	}

	[Fact]
	public void Disconnected_it_shows_what_the_contract_holds_and_says_the_details_wait()
	{
		_links.Set(LinkGroup.Red, Spy);

		var overview = Create();

		Assert.Equal("SPY", overview.Title);
		Assert.Equal("ARCA · USD · Stock", overview.ListingText);
		Assert.Contains("Not connected", overview.DetailsStatus);
		Assert.Equal("Not connected", overview.QuoteStatus);
		Assert.Equal(["Instrument", "Shares"], SectionTitles(overview));
		Assert.Equal("756733", Value(overview, "Contract ID"));
		Assert.Equal(0, _ibkr.SearchCalls);
	}

	[Fact]
	public void Connected_it_loads_the_details_into_the_basic_info_tab()
	{
		_links.Set(LinkGroup.Red, Aapl);
		var overview = Create();

		Connect();
		WaitFor(() => overview.DetailsStatus == null && !overview.IsLoadingDetails);

		Assert.Equal("APPLE INC", overview.NameText);
		Assert.Equal(
			["Instrument", "Classification", "Trading", "Shares"],
			SectionTitles(overview)
		);
		Assert.Equal("APPLE INC", Value(overview, "Name"));
		Assert.Equal("Technology", Value(overview, "Industry"));
		Assert.Equal("US0378331005", Value(overview, "ISIN"));
		Assert.Equal("3 exchanges", Value(overview, "Routes to"));
		Assert.Equal("0.01", Value(overview, "Min tick"));
		Assert.Equal("US/Eastern", Value(overview, "Time zone"));
		// IB left the subcategory blank: no empty row
		Assert.Null(Field(overview, "Subcategory"));
	}

	[Fact]
	public void A_contract_ib_has_no_details_for_says_so()
	{
		var unknown = Stock(999, "ZZZ", "NYSE");
		_links.Set(LinkGroup.Red, unknown);
		var overview = Create();

		Connect();
		WaitFor(() => overview.DetailsStatus?.StartsWith("IB has no details") == true);

		Assert.False(overview.IsLoadingDetails);
		Assert.Equal("ZZZ", Value(overview, "Symbol"));
	}

	[Fact]
	public void A_failed_details_request_shows_the_error()
	{
		_links.Set(LinkGroup.Red, Spy);
		_ibkr.SearchBy = _ => throw new InvalidOperationException("IB is down");
		var overview = Create();

		Connect();
		WaitFor(() => !string.IsNullOrEmpty(overview.ErrorMessage));

		Assert.Null(overview.DetailsStatus);
		Assert.False(overview.IsLoadingDetails);
	}

	[Fact]
	public void It_follows_the_link_group()
	{
		_links.Set(LinkGroup.Red, Spy);
		var overview = Create();
		Connect();

		Ui(() => _links.Set(LinkGroup.Red, Aapl));
		WaitFor(() => overview.NameText == "APPLE INC");

		Assert.Equal("AAPL", overview.Title);
		Assert.Equal("265598", Value(overview, "Contract ID"));
	}

	[Fact]
	public async Task A_typed_ticker_is_shown_and_moves_the_link_group_along()
	{
		_links.Set(LinkGroup.Red, Spy);
		var overview = Create();
		Connect();

		await TestUiThread.Invoke(() => overview.TickerEditor.CommitAsync(" aapl"));
		WaitFor(() => overview.Title == "AAPL");

		Assert.Equal(Aapl.ConId, _links.Get(LinkGroup.Red)?.ConId);
		Assert.Equal(Aapl.ConId, _contracts.GetRecentContracts()[0].ConId);
	}

	[Fact]
	public void A_dropped_ticker_is_shown_and_sent_to_the_link_group()
	{
		_links.Set(LinkGroup.Red, Spy);
		var overview = Create();

		Ui(() => overview.DropContract(Sap));

		Assert.Equal("SAP", overview.Title);
		Assert.Equal("IBIS · EUR · Stock", overview.ListingText);
		Assert.Equal(Sap.ConId, _links.Get(LinkGroup.Red)?.ConId);
		// Not a US stock: no SEC lookup, no Shares section
		Assert.DoesNotContain("Shares", SectionTitles(overview));
	}

	[Fact]
	public void The_contract_and_tab_are_saved_and_a_saved_contract_shows_in_an_empty_group()
	{
		var first = Create();
		Ui(() => first.DropContract(Aapl));
		var placement = new WidgetPlacementDto();
		first.SaveState(placement);
		Ui(first.Dispose);

		var links = new LinkGroupStore();
		var restored = TestUiThread.Invoke(() => Create(links));
		Ui(() => restored.LoadState(placement));

		Assert.Equal(Aapl.ConId, placement.OverviewContract?.ConId);
		Assert.Equal(OverviewTab.BasicInfo, placement.OverviewTab);
		Assert.Equal("AAPL", restored.Title);
		Assert.Equal(Aapl.ConId, links.Get(LinkGroup.Red)?.ConId);
		Ui(restored.Dispose);
	}

	[Fact]
	public void The_link_groups_contract_wins_over_the_saved_one()
	{
		_links.Set(LinkGroup.Red, Spy);
		var overview = Create();

		Ui(() => overview.LoadState(new WidgetPlacementDto { OverviewContract = Aapl }));

		Assert.Equal("SPY", overview.Title);
		Assert.Equal(Spy.ConId, _links.Get(LinkGroup.Red)?.ConId);
	}

	[Fact]
	public void The_quote_takes_one_line_with_the_52_week_range_and_gives_it_back_while_hidden()
	{
		_links.Set(LinkGroup.Red, Spy);
		var overview = Create();
		Connect();
		WaitFor(() => _marketData.ActiveLines == 1);
		WaitFor(() =>
		{
			overview.FlushQuote();
			return overview.PriceText != "—";
		});

		Assert.Equal(MarketDataConstants.AverageVolumeGenericTick, _ibkr.GenericTicksRequested[0]);
		Assert.Equal("600.00", overview.PriceText);
		Assert.Equal("+10.00 (+1.69%)", overview.ChangeText);
		Assert.True(overview.IsUp);
		Assert.Equal(
			"480.00 – 610.00",
			overview.QuoteStats.Single(x => x.Label == "52-week range").Value
		);

		Ui(() => overview.SetShown(false));
		WaitFor(() => _marketData.ActiveLines == 0);
		Assert.Equal("600.00", overview.PriceText);

		Ui(() => overview.SetShown(true));
		WaitFor(() => _marketData.ActiveLines == 1);
	}

	[Fact]
	public void A_refused_quote_line_says_why()
	{
		_links.Set(LinkGroup.Red, Spy);
		_ibkr.MarketDataExceptionFor = _ => new InvalidOperationException(
			"No market data permissions"
		);
		var overview = Create();

		Connect();
		WaitFor(() => overview.QuoteStatus?.StartsWith("No quote") == true);

		Assert.Contains("No market data permissions", overview.QuoteStatus);
	}

	[Fact]
	public void The_market_cap_is_the_sec_share_count_times_the_last_price()
	{
		_shares.Shares[Aapl.Symbol] = 15_000_000_000;
		_links.Set(LinkGroup.Red, Aapl);
		var overview = Create();
		Connect();

		WaitFor(() =>
		{
			overview.FlushQuote();
			return Value(overview, "Market cap") == "9.00T";
		});
		Assert.Equal("15,000,000,000", Value(overview, "Shares outstanding"));
	}

	[Fact]
	public void Switching_contract_drops_the_old_quote_and_line()
	{
		_links.Set(LinkGroup.Red, Spy);
		var overview = Create();
		Connect();
		WaitFor(() =>
		{
			overview.FlushQuote();
			return overview.PriceText != "—";
		});

		Ui(() => _links.Set(LinkGroup.Red, Aapl));

		Assert.Equal("—", overview.PriceText);
		WaitFor(() => _ibkr.MarketDataRequested.Count == 2 && _marketData.ActiveLines == 1);
		Assert.Equal(Aapl.ConId, _ibkr.MarketDataRequested[^1].ConId);
	}

	public void Dispose()
	{
		if (_overview != null)
			TestUiThread.Invoke(_overview.Dispose);
		GC.SuppressFinalize(this);
	}

	private OverviewViewModel Create()
	{
		var overview = TestUiThread.Invoke(() => Create(_links));
		_overview = overview;
		Ui(() => { });
		return overview;
	}

	private OverviewViewModel Create(LinkGroupStore links) =>
		new(_contracts, _marketData, _shares, _connection, links, _cache.Time);

	private void Connect()
	{
		_connection.SetStatus(ConnectionState.Connected, "U1");
		Ui(() => { });
	}

	private void Detail(ContractDto contract, string longName, string industry = "")
	{
		_details[contract.ConId] = new ContractDetailsDto
		{
			Contract = contract,
			LongName = longName,
			Industry = industry,
			MinTick = 0.01,
			TimeZoneId = "US/Eastern",
			TradingHours = "20261023:0400-20261023:2000",
			LiquidHours = "20261023:0930-20261023:1600",
			ValidExchanges = "SMART,NYSE,ARCA",
			SecurityIds = contract == Aapl ? new() { ["ISIN"] = "US0378331005" } : [],
		};
	}

	private static BasicInfoTabViewModel BasicInfo(OverviewViewModel overview) =>
		Assert.IsType<BasicInfoTabViewModel>(overview.Tabs[0]);

	// Read directly: also called from WaitFor's condition, which already runs on the UI thread
	private static List<string> SectionTitles(OverviewViewModel overview) =>
		[.. BasicInfo(overview).Sections.Select(x => x.Title)];

	private static OverviewField? Field(OverviewViewModel overview, string label) =>
		BasicInfo(overview)
			.Sections.SelectMany(x => x.Fields)
			.FirstOrDefault(x => x.Label == label);

	private static string? Value(OverviewViewModel overview, string label) =>
		Field(overview, label)?.Value;

	private static ContractDto Stock(int conId, string symbol, string primaryExchange) =>
		new()
		{
			ConId = conId,
			Symbol = symbol,
			SecurityType = "STK",
			Exchange = "SMART",
			PrimaryExchange = primaryExchange,
			Currency = "USD",
		};

	private static void Ui(Action action) =>
		TestUiThread.Invoke(() =>
		{
			action();
			Dispatcher.UIThread.RunJobs();
		});

	// Runs posted UI work until the condition holds (checked on the UI thread)
	private static void WaitFor(Func<bool> condition)
	{
		var deadline = DateTime.UtcNow.AddSeconds(5);
		while (
			!TestUiThread.Invoke(() =>
			{
				Dispatcher.UIThread.RunJobs();
				return condition();
			})
		)
		{
			if (DateTime.UtcNow > deadline)
				throw new TimeoutException("condition not met");
			Thread.Sleep(10);
		}
	}

	private sealed class StubSharesOutstanding : ISharesOutstandingManager
	{
		public Dictionary<string, long> Shares { get; } = [];

		public async Task<long?> GetSharesOutstandingAsync(
			ContractDto contract,
			CancellationToken cancellationToken = default
		)
		{
			await Task.Yield();
			return Shares.TryGetValue(contract.Symbol, out var shares) ? shares : null;
		}
	}
}
