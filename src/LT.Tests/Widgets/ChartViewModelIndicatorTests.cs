using LT.App.Stores;
using LT.App.Widgets.Chart;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Indicators;
using LT.Contracts.Dtos.Settings;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;
using LT.Domain.Managers;
using LT.Indicators.Scripting;
using LT.Tests.Fakes;

namespace LT.Tests.Widgets;

/// <summary>
/// The chart's indicator manager: it lists the chart's indicators by their legend label, removing
/// one takes it off the chart and out of the saved layout (and the followed template), and a full
/// chart still removes, after which it can add again. A new chart has the volume indicator; a
/// layout saved before volume was an indicator gets it once, and a removal sticks.
/// </summary>
public class ChartViewModelIndicatorTests : IDisposable
{
	private readonly TestCache _cache = new();
	private readonly FakeIbkrApiClient _ibkr = new();
	private readonly FakeConnectionManager _connection = new();
	private readonly IndicatorManager _indicators = new(
		new IndicatorEngine(),
		new InMemoryIndicatorScriptRepository(),
		new MessageLogManager()
	);
	private readonly ChartTemplateManager _templates;
	private readonly List<ChartViewModel> _charts = [];

	public ChartViewModelIndicatorTests() => _templates = new ChartTemplateManager(_cache.Settings);

	[Fact]
	public void Added_indicators_are_listed_by_their_legend_label() =>
		TestUiThread.Invoke(() =>
		{
			var chart = CreateChart();
			var (first, second) = TwoBuiltIns();

			chart.AddIndicator(first);
			chart.AddIndicator(second);

			Assert.True(chart.HasChartIndicators);
			Assert.Equal(
				[Label(IndicatorConstants.VolumeDefinitionId), Label(first), Label(second)],
				chart.ChartIndicators.Select(x => x.Label)
			);
			Assert.Equal(
				State(chart).ChartIndicators!.Select(x => x.Id),
				chart.ChartIndicators.Select(x => x.InstanceId)
			);
		});

	[Fact]
	public void Removing_one_takes_it_off_the_chart_and_the_saved_layout() =>
		TestUiThread.Invoke(() =>
		{
			var chart = CreateEmptyChart();
			var (first, second) = TwoBuiltIns();
			chart.AddIndicator(first);
			chart.AddIndicator(second);

			chart.RemoveIndicator(chart.ChartIndicators[0].InstanceId);

			var remaining = Assert.Single(chart.ChartIndicators);
			Assert.Equal(Label(second), remaining.Label);
			Assert.Equal(second, Assert.Single(State(chart).ChartIndicators!).DefinitionId);

			chart.RemoveIndicator(remaining.InstanceId);

			Assert.Empty(chart.ChartIndicators);
			Assert.False(chart.HasChartIndicators);
			Assert.Null(State(chart).ChartIndicators);
		});

	[Fact]
	public void A_restored_chart_lists_its_indicators_and_removes_them() =>
		TestUiThread.Invoke(() =>
		{
			var (first, _) = TwoBuiltIns();
			var instance = _indicators.CreateInstance(first);
			var missing = new IndicatorInstanceDto
			{
				DefinitionId = IndicatorHelpers.GetScriptId("Gone.cs"),
			};
			var chart = CreateChart();

			chart.LoadState(
				new WidgetPlacementDto
				{
					Tool = WorkspaceTool.Chart,
					ChartIndicators = [instance, missing],
					ChartVolumeMigrated = true,
				}
			);

			Assert.Equal(
				[Label(first), IndicatorHelpers.GetMissingLabel(missing.DefinitionId)],
				chart.ChartIndicators.Select(x => x.Label)
			);
			chart.RemoveIndicator(missing.Id);
			Assert.Equal(instance.Id, Assert.Single(State(chart).ChartIndicators!).Id);
		});

	[Fact]
	public void A_new_chart_has_the_volume_indicator_and_removing_it_sticks() =>
		TestUiThread.Invoke(() =>
		{
			var chart = CreateChart();

			var volume = Assert.Single(chart.ChartIndicators);
			Assert.Equal(Label(IndicatorConstants.VolumeDefinitionId), volume.Label);
			Assert.True(volume.IsVisible);

			chart.RemoveIndicator(volume.InstanceId);
			var saved = State(chart);
			Assert.Null(saved.ChartIndicators);
			Assert.True(saved.ChartVolumeMigrated);

			var restored = CreateChart();
			restored.LoadState(saved);
			Assert.Empty(restored.ChartIndicators);
		});

	[Fact]
	public void A_layout_from_before_volume_was_an_indicator_gets_it_once_first() =>
		TestUiThread.Invoke(() =>
		{
			var (first, _) = TwoBuiltIns();
			var instance = _indicators.CreateInstance(first);
			var chart = CreateChart();

			chart.LoadState(
				new WidgetPlacementDto { Tool = WorkspaceTool.Chart, ChartIndicators = [instance] }
			);

			Assert.Equal(
				[IndicatorConstants.VolumeDefinitionId, first],
				State(chart).ChartIndicators!.Select(x => x.DefinitionId)
			);
			// Loading what it saves doesn't add another
			var again = CreateChart();
			again.LoadState(State(chart));
			Assert.Equal(
				[IndicatorConstants.VolumeDefinitionId, first],
				State(again).ChartIndicators!.Select(x => x.DefinitionId)
			);
		});

	[Fact]
	public void An_old_layout_without_indicators_gets_the_volume_bars() =>
		TestUiThread.Invoke(() =>
		{
			var chart = CreateChart();

			chart.LoadState(new WidgetPlacementDto { Tool = WorkspaceTool.Chart });

			Assert.Equal(
				IndicatorConstants.VolumeDefinitionId,
				Assert.Single(State(chart).ChartIndicators!).DefinitionId
			);
		});

	[Fact]
	public void A_full_chart_can_still_remove_and_then_add_again() =>
		TestUiThread.Invoke(() =>
		{
			var chart = CreateChart();
			var (first, _) = TwoBuiltIns();
			for (var i = 0; i < IndicatorConstants.MaxIndicatorsPerChart; i++)
				chart.AddIndicator(first);
			Assert.False(chart.CanAddIndicator);
			chart.AddIndicator(first);
			Assert.Equal(IndicatorConstants.MaxIndicatorsPerChart, chart.ChartIndicators.Count);
			var changed = new List<string?>();
			chart.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

			chart.RemoveIndicator(chart.ChartIndicators[0].InstanceId);

			Assert.True(chart.CanAddIndicator);
			Assert.Contains(nameof(ChartViewModel.CanAddIndicator), changed);
			chart.AddIndicator(first);
			Assert.Equal(IndicatorConstants.MaxIndicatorsPerChart, chart.ChartIndicators.Count);
		});

	[Fact]
	public void Removing_on_a_chart_following_a_template_saves_to_it() =>
		TestUiThread.Invoke(() =>
		{
			var (first, second) = TwoBuiltIns();
			var id = _templates
				.Save(
					new()
					{
						Name = "Two",
						Settings = new()
						{
							Timeframe = ChartTimeframe.OneHour,
							RightMargin = 0,
							Indicators =
							[
								_indicators.CreateInstance(first),
								_indicators.CreateInstance(second),
							],
						},
					}
				)
				.Id;
			var chart = CreateChart();
			chart.ApplyTemplate(id);
			chart.ToggleFollowTemplate();
			Assert.True(chart.FollowTemplate);

			chart.RemoveIndicator(chart.ChartIndicators[0].InstanceId);
			chart.SaveFollowedTemplate();

			Assert.Equal(
				second,
				Assert.Single(_templates.GetTemplate(id)!.Settings.Indicators!).DefinitionId
			);
		});

	public void Dispose()
	{
		TestUiThread.Invoke(() =>
		{
			foreach (var chart in _charts)
				chart.Dispose();
		});
		GC.SuppressFinalize(this);
	}

	// A new chart without its volume indicator
	private ChartViewModel CreateEmptyChart()
	{
		var chart = CreateChart();
		foreach (var item in chart.ChartIndicators)
			chart.RemoveIndicator(item.InstanceId);
		return chart;
	}

	private (string First, string Second) TwoBuiltIns()
	{
		var definitions = _indicators.GetDefinitions();
		return (definitions[0].Id, definitions[1].Id);
	}

	private string Label(string definitionId) =>
		IndicatorHelpers.GetLabel(
			_indicators.GetDefinition(definitionId)!,
			new Dictionary<string, double>()
		);

	private static WidgetPlacementDto State(ChartViewModel chart)
	{
		var placement = new WidgetPlacementDto { Tool = WorkspaceTool.Chart };
		chart.SaveState(placement);
		return placement;
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
				new MarketDataManager(_ibkr, TimeProvider.System),
				new InMemoryFootprintTapeRepository(),
				_cache.Time
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
			_indicators,
			_templates,
			new PriceAlertManager(
				new InMemoryPriceAlertRepository(),
				new MarketDataManager(_ibkr, TimeProvider.System),
				new MessageLogManager(),
				_cache.Time
			),
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
		return chart;
	}
}
