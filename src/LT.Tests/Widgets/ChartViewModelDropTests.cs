using LT.App.Stores;
using LT.App.Widgets.Chart;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Enums;
using LT.Domain.Managers;
using LT.Indicators.Scripting;
using LT.Tests.Fakes;

namespace LT.Tests.Widgets;

/// <summary>A ticker dragged onto a chart switches that chart only, never its link group.</summary>
public class ChartViewModelDropTests : IDisposable
{
	private static readonly ContractDto Spy = Stock(756733, "SPY");
	private static readonly ContractDto Aapl = Stock(265598, "AAPL");
	private static readonly ContractDto Msft = Stock(272093, "MSFT");

	private readonly TestCache _cache = new();
	private readonly FakeIbkrApiClient _ibkr = new();
	private readonly FakeConnectionManager _connection = new();
	private readonly LinkGroupStore _links = new();
	private readonly ContractManager _contracts;
	private readonly List<ChartViewModel> _charts = [];

	public ChartViewModelDropTests() =>
		_contracts = new ContractManager(_ibkr, _cache.Settings, _cache.Manager);

	[Fact]
	public void Dropping_a_ticker_switches_only_the_chart_it_was_dropped_on()
	{
		var first = CreateChart();
		var second = CreateChart();
		first.OpenContract(Spy);
		Assert.Equal("SPY", second.Title);

		second.DropContract(Aapl);

		Assert.Equal("AAPL", second.Title);
		Assert.Equal("SPY", first.Title);
		Assert.Equal(Spy.ConId, _links.Get(LinkGroup.Red)?.ConId);
	}

	[Fact]
	public void A_dropped_ticker_goes_to_the_recent_tickers_like_a_picked_one()
	{
		var chart = CreateChart();

		chart.DropContract(Aapl);

		Assert.Equal(Aapl.ConId, _contracts.GetRecentContracts()[0].ConId);
	}

	[Fact]
	public void A_chart_with_a_dropped_ticker_still_follows_its_link_group()
	{
		var first = CreateChart();
		var second = CreateChart();
		first.OpenContract(Spy);
		second.DropContract(Aapl);

		first.OpenContract(Msft);

		Assert.Equal("MSFT", second.Title);
		Assert.Equal(LinkGroup.Red, second.Link.Group);
	}

	[Fact]
	public void An_option_dropped_from_the_chain_loads_that_options_trade_bars()
	{
		var chart = TestUiThread.Invoke(CreateChart);
		var call = new ContractDto
		{
			ConId = 701234567,
			Symbol = "SPY",
			LocalSymbol = "SPY   261120C00600000",
			SecurityType = "OPT",
			Exchange = "SMART",
			Currency = "USD",
			LastTradeDateOrContractMonth = "20261120",
			Strike = 600,
			Right = "C",
			Multiplier = "100",
			TradingClass = "SPY",
		};

		TestUiThread.Invoke(() => chart.DropContract(call));

		Assert.Equal("SPY 20261120 600 C", chart.Title);
		WaitFor(() => _ibkr.HistoricalRequests.Any(x => x.Contract.ConId == call.ConId));
		var request = _ibkr.HistoricalRequests.Last(x => x.Contract.ConId == call.ConId);
		Assert.Equal("OPT", request.Contract.SecurityType);
		Assert.Equal("C", request.Contract.Right);
		Assert.Equal(600, request.Contract.Strike);
		Assert.Equal("TRADES", request.WhatToShow);
	}

	public void Dispose()
	{
		foreach (var chart in _charts)
			chart.Dispose();
		GC.SuppressFinalize(this);
	}

	private ChartViewModel CreateChart()
	{
		var settings = new SettingsManager(_cache.Settings, _ibkr);
		var chart = new ChartViewModel(
			new ChartManager(
				_ibkr,
				_cache.Manager,
				new MarketDataManager(_ibkr, TimeProvider.System),
				_cache.Time
			),
			new FootprintManager(
				_ibkr,
				_cache.Manager,
				new MarketDataManager(_ibkr, TimeProvider.System)
			),
			_contracts,
			_connection,
			new MessageLogManager(),
			settings,
			new TradeJournalManager(
				_ibkr,
				new AccountManager(_ibkr, _cache.Manager),
				new InMemoryTradeJournalRepository(),
				_cache.Time
			),
			new PositionManager(_ibkr, _cache.Manager),
			new IndicatorManager(
				new IndicatorEngine(),
				new InMemoryIndicatorScriptRepository(),
				new MessageLogManager()
			),
			new ChartTemplateManager(_cache.Settings),
			new PriceAlertManager(
				new InMemoryPriceAlertRepository(),
				new MarketDataManager(_ibkr, TimeProvider.System),
				new MessageLogManager(),
				_cache.Time
			),
			new ChartDrawingManager(new InMemoryChartDrawingRepository()),
			new ChartDrawingClipboardStore(),
			new AccountStore(_connection, settings),
			_links,
			new DisplayTimeStore(settings, _cache.Time),
			_cache.Time
		);
		_charts.Add(chart);
		return chart;
	}

	private static void WaitFor(Func<bool> condition)
	{
		var until = DateTime.UtcNow.AddSeconds(5);
		while (!TestUiThread.Invoke(condition))
		{
			Assert.True(DateTime.UtcNow < until, "Timed out");
			Thread.Sleep(10);
		}
	}

	private static ContractDto Stock(int conId, string symbol) =>
		new()
		{
			ConId = conId,
			Symbol = symbol,
			SecurityType = "STK",
			Exchange = "SMART",
			Currency = "USD",
		};
}
