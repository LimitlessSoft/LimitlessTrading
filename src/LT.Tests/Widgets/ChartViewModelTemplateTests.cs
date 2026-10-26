using LT.App.Stores;
using LT.App.Widgets.Chart;
using LT.Contracts.Dtos.ChartTemplates;
using LT.Contracts.Dtos.Settings;
using LT.Contracts.Enums;
using LT.Domain.Managers;
using LT.Indicators.Scripting;
using LT.Tests.Fakes;

namespace LT.Tests.Widgets;

/// <summary>
/// Chart templates from the chart's side: a setup saved on one chart is applied to another, a
/// taken name asks before replacing, the default template sets up new charts but not restored ones.
/// </summary>
public class ChartViewModelTemplateTests : IDisposable
{
	private readonly TestCache _cache = new();
	private readonly FakeIbkrApiClient _ibkr = new();
	private readonly FakeConnectionManager _connection = new();
	private readonly LinkGroupStore _links = new();
	private readonly IndicatorManager _indicators = new(
		new IndicatorEngine(),
		new InMemoryIndicatorScriptRepository(),
		new MessageLogManager()
	);
	private readonly ChartTemplateManager _templates;
	private readonly List<ChartViewModel> _charts = [];

	public ChartViewModelTemplateTests() => _templates = new ChartTemplateManager(_cache.Settings);

	[Fact]
	public void A_setup_saved_on_one_chart_is_applied_to_another() =>
		TestUiThread.Invoke(() =>
		{
			var source = CreateChart();
			var (first, second) = TwoBuiltIns();
			source.SetTimeframe(ChartTimeframe.OneHour);
			source.RightMargin = 8;
			source.AddIndicator(first);
			source.AddIndicator(second);
			source.NewTemplateName = "Hourly";
			source.SaveTemplate();
			var target = CreateChart();
			target.SetTimeframe(ChartTimeframe.OneMinute);

			target.ApplyTemplate(_templates.FindByName("Hourly")!.Id);

			var applied = State(target);
			var original = State(source);
			Assert.Equal(ChartTimeframe.OneHour, applied.ChartTimeframe);
			Assert.Equal(8, applied.ChartRightMargin);
			Assert.Equal(
				original.ChartIndicators!.Select(x => (x.DefinitionId, x.Inputs.Count)),
				applied.ChartIndicators!.Select(x => (x.DefinitionId, x.Inputs.Count))
			);
			// Own instances, not the source chart's
			Assert.Empty(
				applied
					.ChartIndicators!.Select(x => x.Id)
					.Intersect(original.ChartIndicators!.Select(x => x.Id))
			);
			Assert.Equal("Saved Hourly", source.TemplateMessage);
			Assert.Null(source.NewTemplateName);
		});

	[Fact]
	public void Saving_under_a_taken_name_asks_and_replaces_only_when_confirmed() =>
		TestUiThread.Invoke(() =>
		{
			var chart = CreateChart();
			chart.SetTimeframe(ChartTimeframe.OneMinute);
			chart.NewTemplateName = "Mine";
			chart.SaveTemplate();
			chart.SetTimeframe(ChartTimeframe.OneDay);

			chart.NewTemplateName = "mine";
			chart.SaveTemplate();

			Assert.True(chart.IsConfirmingOverwrite);
			Assert.Equal(ChartTimeframe.OneMinute, Single().Settings.Timeframe);

			chart.CancelOverwrite();
			Assert.False(chart.IsConfirmingOverwrite);
			Assert.Equal(ChartTimeframe.OneMinute, Single().Settings.Timeframe);

			chart.SaveTemplate();
			chart.ConfirmOverwrite();

			Assert.False(chart.IsConfirmingOverwrite);
			Assert.Equal(ChartTimeframe.OneDay, Single().Settings.Timeframe);
		});

	[Fact]
	public void A_blank_name_shows_the_error_in_the_menu() =>
		TestUiThread.Invoke(() =>
		{
			var chart = CreateChart();
			chart.NewTemplateName = "  ";

			chart.SaveTemplate();

			Assert.True(chart.IsTemplateMessageError);
			Assert.Equal("Template name is required.", chart.TemplateMessage);
			Assert.Empty(_templates.GetTemplates());
		});

	[Fact]
	public void The_default_template_sets_up_a_new_chart() =>
		TestUiThread.Invoke(() =>
		{
			var source = CreateChart();
			source.SetTimeframe(ChartTimeframe.FifteenMinutes);
			source.AddIndicator(TwoBuiltIns().First);
			source.NewTemplateName = "Default";
			source.SaveTemplate();
			_templates.SetDefault(_templates.FindByName("Default")!.Id);

			var fresh = State(CreateChart());

			Assert.Equal(ChartTimeframe.FifteenMinutes, fresh.ChartTimeframe);
			Assert.Single(fresh.ChartIndicators!);
		});

	[Fact]
	public void A_restored_chart_keeps_its_own_setup_over_the_default_template() =>
		TestUiThread.Invoke(() =>
		{
			var source = CreateChart();
			source.SetTimeframe(ChartTimeframe.FifteenMinutes);
			source.AddIndicator(TwoBuiltIns().First);
			source.NewTemplateName = "Default";
			source.SaveTemplate();
			_templates.SetDefault(_templates.FindByName("Default")!.Id);

			var restored = CreateChart();
			restored.LoadState(
				new WidgetPlacementDto
				{
					Tool = WorkspaceTool.Chart,
					ChartTimeframe = ChartTimeframe.OneDay,
					ChartLinked = true,
				}
			);

			var state = State(restored);
			Assert.Equal(ChartTimeframe.OneDay, state.ChartTimeframe);
			Assert.Null(state.ChartIndicators);
		});

	[Fact]
	public void A_template_without_indicators_leaves_the_charts_own() =>
		TestUiThread.Invoke(() =>
		{
			var chart = CreateChart();
			chart.AddIndicator(TwoBuiltIns().First);
			_templates.Save(
				new()
				{
					Name = "Timeframe only",
					Settings = new() { Timeframe = ChartTimeframe.OneWeek },
				}
			);

			chart.ApplyTemplate(_templates.FindByName("Timeframe only")!.Id);

			var state = State(chart);
			Assert.Equal(ChartTimeframe.OneWeek, state.ChartTimeframe);
			Assert.Single(state.ChartIndicators!);
		});

	[Fact]
	public void Importing_a_file_lists_the_templates_and_says_so() =>
		TestUiThread.Invoke(() =>
		{
			var chart = CreateChart();
			chart.NewTemplateName = "Mine";
			chart.SaveTemplate();
			var json = chart.ExportTemplates(null);

			chart.ImportTemplates(json);

			Assert.Equal(["Mine", "Mine (2)"], chart.Templates.Select(x => x.Name));
			Assert.Equal("Imported Mine (2)", chart.TemplateMessage);
			Assert.False(chart.IsTemplateMessageError);
		});

	[Fact]
	public void Importing_an_invalid_file_shows_why_in_the_menu() =>
		TestUiThread.Invoke(() =>
		{
			var chart = CreateChart();

			chart.ImportTemplates("{ \"hello\": 1 }");

			Assert.True(chart.IsTemplateMessageError);
			Assert.Equal("This file is not a chart template file.", chart.TemplateMessage);
			Assert.Empty(chart.Templates);
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

	private ChartTemplateDto Single() => Assert.Single(_templates.GetTemplates());

	private (string First, string Second) TwoBuiltIns()
	{
		var definitions = _indicators.GetDefinitions();
		return (definitions[0].Id, definitions[1].Id);
	}

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
			new ChartManager(_ibkr, _cache.Manager),
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
			new AccountStore(_connection, settings),
			_links,
			new DisplayTimeStore(settings, _cache.Time)
		);
		_charts.Add(chart);
		return chart;
	}
}
