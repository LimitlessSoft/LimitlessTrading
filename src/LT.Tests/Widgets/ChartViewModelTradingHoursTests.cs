using Avalonia.Threading;
using LT.App.Stores;
using LT.App.Widgets.Chart;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Enums;
using LT.Contracts.Requests.Settings;
using LT.Domain.Managers;
using LT.Indicators.Scripting;
using LT.Tests.Fakes;

namespace LT.Tests.Widgets;

/// <summary>
/// Charts follow the "pre- and post-market bars" setting: on by default (IB useRTH=false), and
/// switching it reloads the open charts without reopening them.
/// </summary>
public class ChartViewModelTradingHoursTests : IDisposable
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

	public ChartViewModelTradingHoursTests() =>
		_settings = new SettingsManager(_cache.Settings, _ibkr);

	[Fact]
	public void A_new_chart_asks_for_bars_outside_regular_hours()
	{
		var chart = TestUiThread.Invoke(CreateChart);

		Assert.False(chart.UseRegularTradingHours);
		WaitFor(() => _ibkr.HistoricalUseRth.Count > 0);
		Assert.False(_ibkr.HistoricalUseRth[^1]);
	}

	[Fact]
	public void Turning_the_setting_off_reloads_open_charts_with_regular_hours_only()
	{
		var chart = TestUiThread.Invoke(CreateChart);
		WaitFor(() => _ibkr.HistoricalUseRth.Count > 0);

		_settings.SaveAppSettings(
			new SaveAppSettingsRequest
			{
				MarketDataType = MarketDataType.Delayed,
				HeartbeatSeconds = 10,
				ChartOutsideRegularHours = false,
			}
		);
		TestUiThread.Invoke(() => Dispatcher.UIThread.RunJobs());

		Assert.True(chart.UseRegularTradingHours);
		WaitFor(() => _ibkr.HistoricalUseRth.Contains(true));
		Assert.Null(chart.RegularHours);
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
			new ChartManager(_ibkr, _cache.Manager, _cache.Time),
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
			new AccountStore(_connection, _settings),
			new LinkGroupStore(),
			new DisplayTimeStore(_settings, _cache.Time)
		);
		_charts.Add(chart);
		chart.OpenContract(Spy);
		return chart;
	}
}
