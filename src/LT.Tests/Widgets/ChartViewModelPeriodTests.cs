using Avalonia.Threading;
using LT.App.Stores;
using LT.App.Widgets.Chart;
using LT.Contracts.Dtos.Charts;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Enums;
using LT.Domain.Managers;
using LT.Indicators.Scripting;
using LT.Tests.Fakes;

namespace LT.Tests.Widgets;

/// <summary>
/// Picking a timeframe or a period in the chart's header reloads its bars with them, a chosen
/// period is shown whole, and a load another pick superseded never shows its bars.
/// </summary>
public class ChartViewModelPeriodTests : IDisposable
{
	private static readonly ContractDto Spy = new()
	{
		ConId = 756733,
		Symbol = "SPY",
		SecurityType = "STK",
		Exchange = "SMART",
		Currency = "USD",
	};

	private readonly TestCache _cache = new();
	private readonly FakeIbkrApiClient _ibkr = new();
	private readonly FakeConnectionManager _connection = new();
	private readonly SettingsManager _settings;
	private readonly List<ChartViewModel> _charts = [];

	public ChartViewModelPeriodTests()
	{
		_settings = new SettingsManager(_cache.Settings, _ibkr);
		// Each answer tells which request it was: its close is the request's number
		_ibkr.HistoricalBy = (_, _) =>
			Task.FromResult<IReadOnlyList<BarDto>>([Bar(_ibkr.HistoricalCalls)]);
	}

	[Fact]
	public void Picking_a_period_reloads_the_bars_for_it()
	{
		var chart = TestUiThread.Invoke(CreateChart);
		WaitForClose(chart, 1);
		var auto = _ibkr.HistoricalDurations[^1];

		TestUiThread.Invoke(() => chart.SetPeriod(ChartPeriod.SevenDays));

		WaitForClose(chart, 2);
		Assert.NotEqual(auto, _ibkr.HistoricalDurations[^1]);
		Assert.True(chart.FitsPeriod);
	}

	[Fact]
	public void Picking_a_timeframe_reloads_the_bars_at_that_size()
	{
		var chart = TestUiThread.Invoke(CreateChart);
		WaitForClose(chart, 1);

		TestUiThread.Invoke(() => chart.SetTimeframe(ChartTimeframe.OneHour));

		WaitForClose(chart, 2);
		Assert.Equal("1 hour", _ibkr.HistoricalBarSizes[^1]);
	}

	[Fact]
	public void Auto_keeps_the_latest_bars_at_the_default_zoom()
	{
		var chart = TestUiThread.Invoke(CreateChart);

		Assert.False(chart.FitsPeriod);
		TestUiThread.Invoke(() => chart.SetPeriod(ChartPeriod.OneMonth));
		Assert.True(chart.FitsPeriod);
		TestUiThread.Invoke(() => chart.SetPeriod(ChartPeriod.Auto));
		Assert.False(chart.FitsPeriod);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public void A_superseded_load_never_replaces_the_newer_bars(bool oldLoadFails)
	{
		// The first load's answer is still on its way when another timeframe is picked
		var first = new TaskCompletionSource<IReadOnlyList<BarDto>>();
		_ibkr.HistoricalBy = (_, barSize) =>
			barSize == "1 hour" ? Task.FromResult<IReadOnlyList<BarDto>>([Bar(60)]) : first.Task;
		var chart = TestUiThread.Invoke(CreateChart);
		WaitFor(() => _ibkr.HistoricalCalls == 1);

		TestUiThread.Invoke(() => chart.SetTimeframe(ChartTimeframe.OneHour));
		WaitForClose(chart, 60);

		// IB answers the cancelled request anyway (or fails it)
		if (oldLoadFails)
			first.SetException(new InvalidOperationException("Historical data farm down"));
		else
			first.SetResult([Bar(5)]);
		TestUiThread.Invoke(() => Dispatcher.UIThread.RunJobs());
		Thread.Sleep(50);
		TestUiThread.Invoke(() => Dispatcher.UIThread.RunJobs());

		TestUiThread.Invoke(() =>
		{
			Assert.Equal(60, Assert.Single(chart.Bars).Close);
			Assert.Null(chart.ErrorMessage);
		});
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

	// Within the last day: every period keeps it
	private BarDto Bar(double close) =>
		new()
		{
			Time = _cache.Time.GetUtcNow().AddMinutes(-30),
			Open = close,
			High = close,
			Low = close,
			Close = close,
			Volume = 100,
		};

	private static void WaitForClose(ChartViewModel chart, double close) =>
		WaitFor(() =>
			TestUiThread.Invoke(() =>
			{
				Dispatcher.UIThread.RunJobs();
				return chart.Bars is [var bar] && bar.Close == close;
			})
		);

	private static void WaitFor(Func<bool> condition)
	{
		var until = DateTime.UtcNow.AddSeconds(5);
		while (!condition())
		{
			Assert.True(DateTime.UtcNow < until, "Timed out");
			Thread.Sleep(10);
		}
	}

	private ChartViewModel CreateChart()
	{
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
			_settings,
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
			new AccountStore(_connection, _settings),
			new LinkGroupStore(),
			new DisplayTimeStore(_settings, _cache.Time),
			_cache.Time
		);
		_charts.Add(chart);
		chart.OpenContract(Spy);
		return chart;
	}
}
