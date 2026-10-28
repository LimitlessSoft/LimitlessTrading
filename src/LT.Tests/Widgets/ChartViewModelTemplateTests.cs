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
/// taken name asks before replacing, the default template sets up new charts but not restored ones;
/// charts following a template save their edits to it and take each other's, and turning following
/// on asks first when the chart differs from the template.
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

	[Fact]
	public void Following_charts_take_each_others_edits_and_others_are_left_alone() =>
		TestUiThread.Invoke(() =>
		{
			var id = SaveHourly();
			var (first, _) = TwoBuiltIns();
			var editor = Following(id);
			var follower = Following(id);
			var bystander = CreateChart();
			bystander.ApplyTemplate(id);

			editor.SetTimeframe(ChartTimeframe.FiveMinutes);
			editor.AddIndicator(first);
			editor.RightMargin = 3;

			// Debounced: nothing saved until the edits stop
			Assert.Equal(ChartTimeframe.OneHour, _templates.GetTemplate(id)!.Settings.Timeframe);
			editor.SaveFollowedTemplate();

			var template = _templates.GetTemplate(id)!;
			Assert.Equal("Hourly", template.Name);
			Assert.Equal(ChartTimeframe.FiveMinutes, template.Settings.Timeframe);
			Assert.Equal(3, template.Settings.RightMargin);
			Assert.Equal(first, Assert.Single(template.Settings.Indicators!).DefinitionId);
			var followed = State(follower);
			Assert.Equal(ChartTimeframe.FiveMinutes, followed.ChartTimeframe);
			Assert.Equal(3, followed.ChartRightMargin);
			Assert.Equal(first, Assert.Single(followed.ChartIndicators!).DefinitionId);
			var untouched = State(bystander);
			Assert.Equal(ChartTimeframe.OneHour, untouched.ChartTimeframe);
			Assert.Null(untouched.ChartIndicators);
		});

	[Fact]
	public void A_saved_setup_holds_the_period_and_followers_share_it() =>
		TestUiThread.Invoke(() =>
		{
			var source = CreateChart();
			source.SetPeriod(ChartPeriod.OneMonth);
			source.NewTemplateName = "Monthly";
			source.SaveTemplate();
			var id = _templates.FindByName("Monthly")!.Id;
			Assert.Equal(ChartPeriod.OneMonth, _templates.GetTemplate(id)!.Settings.Period);
			var editor = Following(id);
			var follower = Following(id);
			var bystander = CreateChart();
			Assert.Equal(ChartPeriod.OneMonth, State(follower).ChartPeriod);

			editor.SetPeriod(ChartPeriod.ThisWeek);
			editor.SaveFollowedTemplate();

			Assert.Equal(ChartPeriod.ThisWeek, _templates.GetTemplate(id)!.Settings.Period);
			Assert.Equal(ChartPeriod.ThisWeek, State(follower).ChartPeriod);
			Assert.Equal(ChartPeriod.Auto, State(bystander).ChartPeriod);
		});

	[Fact]
	public void Followers_of_a_template_from_before_periods_share_their_period() =>
		TestUiThread.Invoke(() =>
		{
			// The template as version 1 stored it: a timeframe, no period
			_cache.Settings.Settings.ChartTemplates =
			[
				new()
				{
					Id = "old",
					Name = "5min",
					Version = 1,
					Settings = new() { Timeframe = ChartTimeframe.FiveMinutes },
				},
			];
			var editor = Following("old");
			var follower = Following("old");

			editor.SetPeriod(ChartPeriod.SevenDays);
			editor.SaveFollowedTemplate();

			Assert.Equal(ChartPeriod.SevenDays, State(follower).ChartPeriod);
		});

	[Fact]
	public void Applying_a_followed_save_is_not_saved_back() =>
		TestUiThread.Invoke(() =>
		{
			var id = SaveHourly();
			var editor = Following(id);
			var follower = Following(id);
			var saves = 0;
			_templates.TemplateSaved += _ => saves++;

			editor.AddIndicator(TwoBuiltIns().First);
			var instance = Assert.Single(State(editor).ChartIndicators!).Id;
			editor.SaveFollowedTemplate();
			follower.SaveFollowedTemplate();
			editor.SaveFollowedTemplate();

			Assert.Equal(1, saves);
			// The editor ignored its own save: still its own instance, not a copy
			Assert.Equal(instance, Assert.Single(State(editor).ChartIndicators!).Id);
			Assert.Single(State(follower).ChartIndicators!);
		});

	[Fact]
	public void A_chart_not_following_keeps_its_edits_to_itself() =>
		TestUiThread.Invoke(() =>
		{
			var id = SaveHourly();
			var chart = CreateChart();
			chart.ApplyTemplate(id);

			chart.SetTimeframe(ChartTimeframe.OneDay);
			chart.SaveFollowedTemplate();

			Assert.Equal(ChartTimeframe.OneHour, _templates.GetTemplate(id)!.Settings.Timeframe);
		});

	[Fact]
	public void Following_an_outdated_template_asks_and_cancel_leaves_the_chart_as_is() =>
		TestUiThread.Invoke(() =>
		{
			var id = SaveHourly();
			var chart = CreateChart();
			chart.ApplyTemplate(id);
			chart.SetTimeframe(ChartTimeframe.OneDay);

			chart.ToggleFollowTemplate();

			Assert.True(chart.IsConfirmingFollow);
			Assert.False(chart.FollowTemplate);
			Assert.Contains("outdated version of template \"Hourly\"", chart.FollowPrompt);

			chart.CancelFollow();

			Assert.False(chart.IsConfirmingFollow);
			Assert.False(chart.FollowTemplate);
			Assert.Equal(ChartTimeframe.OneDay, State(chart).ChartTimeframe);
		});

	[Fact]
	public void Following_an_outdated_template_when_confirmed_updates_the_chart_first() =>
		TestUiThread.Invoke(() =>
		{
			var id = SaveHourly();
			var chart = CreateChart();
			chart.ApplyTemplate(id);
			chart.SetTimeframe(ChartTimeframe.OneDay);
			chart.ToggleFollowTemplate();

			chart.ConfirmFollow();

			Assert.False(chart.IsConfirmingFollow);
			Assert.True(chart.FollowTemplate);
			Assert.Equal(ChartTimeframe.OneHour, State(chart).ChartTimeframe);
			// The template was not touched by the update
			Assert.Equal(ChartTimeframe.OneHour, _templates.GetTemplate(id)!.Settings.Timeframe);
		});

	[Fact]
	public void Following_a_template_the_chart_matches_needs_no_question() =>
		TestUiThread.Invoke(() =>
		{
			var id = SaveHourly();
			var chart = CreateChart();
			chart.ApplyTemplate(id);

			chart.ToggleFollowTemplate();

			Assert.True(chart.FollowTemplate);
			Assert.False(chart.IsConfirmingFollow);

			chart.ToggleFollowTemplate();

			Assert.False(chart.FollowTemplate);
		});

	[Fact]
	public void Without_a_template_following_does_nothing() =>
		TestUiThread.Invoke(() =>
		{
			SaveHourly();
			var chart = CreateChart();

			chart.ToggleFollowTemplate();

			Assert.False(chart.HasAppliedTemplate);
			Assert.False(chart.FollowTemplate);
			Assert.False(chart.IsConfirmingFollow);
		});

	[Fact]
	public void A_restored_follower_takes_the_templates_latest_version() =>
		TestUiThread.Invoke(() =>
		{
			var id = SaveHourly();
			var saved = State(Following(id));
			Assert.Equal(id, saved.ChartTemplateId);
			Assert.True(saved.ChartFollowTemplate);
			_templates.UpdateSettings(
				new()
				{
					Id = id,
					Settings = new() { Timeframe = ChartTimeframe.OneWeek },
				}
			);

			var restored = CreateChart();
			saved.ChartTimeframe = ChartTimeframe.OneHour;
			restored.LoadState(saved);

			Assert.True(restored.FollowTemplate);
			Assert.Equal("Hourly", restored.AppliedTemplateName);
			Assert.Equal(ChartTimeframe.OneWeek, State(restored).ChartTimeframe);
		});

	[Fact]
	public void Deleting_the_followed_template_stops_following() =>
		TestUiThread.Invoke(() =>
		{
			var id = SaveHourly();
			var chart = Following(id);
			_templates.Delete(id);

			chart.SetTimeframe(ChartTimeframe.OneDay);
			chart.SaveFollowedTemplate();

			Assert.False(chart.FollowTemplate);
			Assert.False(chart.HasAppliedTemplate);
			Assert.Empty(_templates.GetTemplates());
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

	// A template "Hourly": 1 hour, no margin, no indicators
	private string SaveHourly() =>
		_templates
			.Save(
				new()
				{
					Name = "Hourly",
					Settings = new()
					{
						Timeframe = ChartTimeframe.OneHour,
						RightMargin = 0,
						Indicators = [],
					},
				}
			)
			.Id;

	private ChartViewModel Following(string templateId)
	{
		var chart = CreateChart();
		chart.ApplyTemplate(templateId);
		chart.ToggleFollowTemplate();
		Assert.True(chart.FollowTemplate);
		return chart;
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
			new ChartManager(
				_ibkr,
				_cache.Manager,
				new MarketDataManager(_ibkr, TimeProvider.System),
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
			new AccountStore(_connection, settings),
			_links,
			new DisplayTimeStore(settings, _cache.Time),
			_cache.Time
		);
		_charts.Add(chart);
		return chart;
	}
}
