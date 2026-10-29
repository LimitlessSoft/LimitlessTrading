using Avalonia.Threading;
using LT.App.Stores;
using LT.App.Widgets.Chart;
using LT.Contracts.Dtos.Charts;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Enums;
using LT.Contracts.Requests.PriceAlerts;
using LT.Domain.Managers;
using LT.Indicators.Scripting;
using LT.Tests.Fakes;

namespace LT.Tests.Widgets;

/// <summary>
/// A chart shows its contract's price alerts and adds, moves and removes them; the direction is
/// told from its last bar's close.
/// </summary>
public class ChartViewModelAlertTests : IDisposable
{
	private static readonly ContractDto Spy = Stock(756733, "SPY");
	private static readonly ContractDto Aapl = Stock(265598, "AAPL");

	private readonly TestCache _cache = new();
	private readonly FakeIbkrApiClient _ibkr = new();
	private readonly FakeConnectionManager _connection = new();
	private readonly PriceAlertManager _alerts;
	private readonly List<ChartViewModel> _charts = [];

	public ChartViewModelAlertTests()
	{
		_alerts = new PriceAlertManager(
			new InMemoryPriceAlertRepository(),
			new MarketDataManager(_ibkr, TimeProvider.System),
			new MessageLogManager(),
			_cache.Time
		);
		_ibkr.HistoricalBars =
		[
			new BarDto
			{
				Time = _cache.Time.GetUtcNow().AddDays(-1),
				Open = 180,
				High = 186,
				Low = 179,
				Close = 185,
			},
		];
	}

	[Fact]
	public void An_alert_above_the_last_close_is_added_on_the_way_up()
	{
		var chart = TestUiThread.Invoke(() => CreateChart(Spy));
		WaitFor(() => chart.LastPrice != null);

		TestUiThread.Invoke(() =>
		{
			chart.AddAlert(190);
			Dispatcher.UIThread.RunJobs();
		});

		var alert = Assert.Single(chart.Alerts);
		Assert.Equal(190, alert.Price);
		Assert.Equal(PriceAlertCondition.CrossingUp, alert.Condition);
		Assert.Equal(Spy.ConId, alert.Contract.ConId);
	}

	[Fact]
	public void Without_bars_no_alert_is_added()
	{
		_ibkr.HistoricalBars = [];
		var chart = TestUiThread.Invoke(() => CreateChart(Spy));

		TestUiThread.Invoke(() => chart.AddAlert(190));

		Assert.False(chart.CanAddAlert);
		Assert.Empty(_alerts.GetAlerts());
	}

	[Fact]
	public void A_chart_shows_only_its_contracts_alerts_and_follows_a_switch()
	{
		var chart = TestUiThread.Invoke(() => CreateChart(Spy));
		WaitFor(() => chart.LastPrice != null);
		TestUiThread.Invoke(() => chart.AddAlert(180));
		Add(Aapl, 250);

		TestUiThread.Invoke(() => Dispatcher.UIThread.RunJobs());
		Assert.Equal([180.0], chart.Alerts.Select(x => x.Price));

		TestUiThread.Invoke(() => chart.DropContract(Aapl));
		Assert.Equal([250.0], chart.Alerts.Select(x => x.Price));
	}

	[Fact]
	public async Task An_alert_fired_off_the_ui_thread_shows_as_triggered()
	{
		var chart = TestUiThread.Invoke(() => CreateChart(Spy));
		WaitFor(() => chart.LastPrice != null);
		TestUiThread.Invoke(() => chart.AddAlert(190));

		await Task.Run(
			() => _alerts.OnPrice(Spy.ConId, 191),
			TestContext.Current.CancellationToken
		);
		WaitFor(() => chart.HasTriggeredAlerts);

		Assert.True(Assert.Single(chart.Alerts).IsTriggered);
	}

	[Fact]
	public void Dragging_and_removing_reach_the_alert()
	{
		var chart = TestUiThread.Invoke(() => CreateChart(Spy));
		WaitFor(() => chart.LastPrice != null);
		TestUiThread.Invoke(() =>
		{
			chart.AddAlert(190);
			Dispatcher.UIThread.RunJobs();
		});
		var id = chart.Alerts[0].Id;

		TestUiThread.Invoke(() =>
		{
			chart.MoveAlert(id, 175);
			Dispatcher.UIThread.RunJobs();
		});
		var moved = Assert.Single(chart.Alerts);
		Assert.Equal(175, moved.Price);
		Assert.Equal(PriceAlertCondition.CrossingDown, moved.Condition);

		TestUiThread.Invoke(() =>
		{
			chart.RemoveAlert(id);
			Dispatcher.UIThread.RunJobs();
		});
		Assert.Empty(chart.Alerts);
	}

	public void Dispose()
	{
		TestUiThread.Invoke(() =>
		{
			foreach (var chart in _charts)
				chart.Dispose();
		});
		GC.SuppressFinalize(this);
	}

	private void Add(ContractDto contract, double price) =>
		_alerts.Create(
			new CreatePriceAlertRequest
			{
				Contract = contract,
				Price = price,
				CurrentPrice = price - 1,
			}
		);

	// Runs the UI thread's posted jobs until the condition holds
	private static void WaitFor(Func<bool> condition)
	{
		var until = DateTime.UtcNow.AddSeconds(5);
		while (true)
		{
			TestUiThread.Invoke(() => Dispatcher.UIThread.RunJobs());
			if (condition())
				return;
			Assert.True(DateTime.UtcNow < until, "Timed out");
			Thread.Sleep(10);
		}
	}

	private ChartViewModel CreateChart(ContractDto contract)
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
			new ContractManager(_ibkr, _cache.Settings, _cache.Manager),
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
			_alerts,
			new ChartDrawingManager(new InMemoryChartDrawingRepository()),
			new OrderManager(_ibkr, _cache.Manager),
			new ChartDrawingClipboardStore(),
			new OpenOrdersStore(new OrderManager(_ibkr, _cache.Manager), _connection, _cache.Time),
			new AccountStore(_connection, settings),
			new LinkGroupStore(),
			new DisplayTimeStore(settings, _cache.Time),
			_cache.Time
		);
		_charts.Add(chart);
		chart.OpenContract(contract);
		return chart;
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
