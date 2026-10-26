using LT.App.Stores;
using LT.App.Workspace;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Settings;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;
using LT.Domain.Managers;
using LT.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;

namespace LT.Tests.Workspace;

/// <summary>
/// Reordering views (center tabs), moving them to another workspace window and detaching them into
/// a new one. Widget view models are plain objects: only where they end up matters here.
/// </summary>
public class WorkspaceViewMoveTests
{
	private static WorkspaceViewModel CreateWorkspace(params string[] viewNames)
	{
		var workspace = new WorkspaceViewModel(_ => new object(), new WorkspaceLayoutDto());
		workspace.Views[0].Name = viewNames.FirstOrDefault() ?? WorkspaceConstants.DefaultViewName;
		foreach (var name in viewNames.Skip(1))
		{
			workspace.AddView();
			workspace.SelectedView.Name = name;
		}
		return workspace;
	}

	private static WorkspaceWindows CreateWindows()
	{
		LSCoreContainer.EnsureInitialized();
		var settings = new SettingsManager(
			new InMemorySettingsRepository(),
			new FakeIbkrApiClient()
		);
		// An empty main window: no first-run widgets
		settings.SaveLayout(new LayoutDto());
		var services = new ServiceCollection();
		foreach (var definition in WidgetCatalog.Definitions)
			services.AddTransient(definition.ViewModelType, _ => new object());
		return new WorkspaceWindows(
			new WidgetViewModelFactory(services.BuildServiceProvider()),
			settings,
			new AppearanceStore(settings)
		);
	}

	private static List<string> Names(WorkspaceViewModel workspace) =>
		workspace.Views.Select(x => x.Name).ToList();

	private static CenterViewItem View(WorkspaceViewModel workspace, string name) =>
		workspace.Views.Single(x => x.Name == name);

	private static WidgetItem SpawnChart(WorkspaceViewModel workspace, string viewName)
	{
		workspace.SelectView(View(workspace, viewName));
		workspace.Spawn(WorkspaceTool.Chart);
		return workspace.Widgets.Last();
	}

	[Fact]
	public void Dragging_a_tab_right_puts_it_in_the_gap_it_was_dropped_on()
	{
		var workspace = CreateWorkspace("A", "B", "C", "D");

		// Gap 3 is between C and D
		workspace.MoveView(View(workspace, "A"), 3);

		Assert.Equal(["B", "C", "A", "D"], Names(workspace));
	}

	[Fact]
	public void Dragging_a_tab_left_puts_it_in_the_gap_it_was_dropped_on()
	{
		var workspace = CreateWorkspace("A", "B", "C", "D");

		workspace.MoveView(View(workspace, "D"), 1);

		Assert.Equal(["A", "D", "B", "C"], Names(workspace));
	}

	[Fact]
	public void Dropping_a_tab_next_to_itself_leaves_the_order_alone()
	{
		var workspace = CreateWorkspace("A", "B", "C");

		workspace.MoveView(View(workspace, "B"), 1);
		workspace.MoveView(View(workspace, "B"), 2);

		Assert.Equal(["A", "B", "C"], Names(workspace));
	}

	[Fact]
	public void Moving_a_view_keeps_its_widgets_and_is_saved_in_the_new_order()
	{
		var workspace = CreateWorkspace("A", "B");
		var chart = SpawnChart(workspace, "A");

		workspace.MoveView(View(workspace, "A"), 2);

		var layout = new WorkspaceLayoutDto();
		workspace.FillLayout(layout);
		Assert.Equal(["B", "A"], layout.Views.Select(x => x.Name));
		Assert.Equal([chart.Id], CenterLayoutHelpers.GetWidgetIds(layout.Views[1].Root));
		Assert.Equal(WidgetDock.Center, chart.Dock);
	}

	[Fact]
	public void Move_left_and_right_step_one_tab_and_stop_at_the_ends()
	{
		var workspace = CreateWorkspace("A", "B", "C");

		workspace.MoveViewBy(View(workspace, "B"), -1);
		Assert.Equal(["B", "A", "C"], Names(workspace));
		workspace.MoveViewBy(View(workspace, "B"), -1);
		Assert.Equal(["B", "A", "C"], Names(workspace));
		workspace.MoveViewBy(View(workspace, "C"), 1);
		Assert.Equal(["B", "A", "C"], Names(workspace));
		workspace.MoveViewBy(View(workspace, "A"), 1);
		Assert.Equal(["B", "C", "A"], Names(workspace));
	}

	[Fact]
	public void Only_tabs_with_a_neighbour_on_that_side_offer_moving_there()
	{
		var workspace = CreateWorkspace("A", "B", "C");

		workspace.MoveView(View(workspace, "C"), 0);

		Assert.Equal(
			[(false, true), (true, true), (true, false)],
			workspace.Views.Select(x => (x.CanMoveLeft, x.CanMoveRight))
		);
	}

	[Fact]
	public void Moving_a_view_to_another_window_takes_its_widgets_along_and_shows_it_there()
	{
		var windows = CreateWindows();
		var main = windows.Main;
		main.Views[0].Name = "A";
		main.AddView();
		main.SelectedView.Name = "B";
		var chart = SpawnChart(main, "B");
		var content = chart.Content;
		var other = windows.Open().Workspace;

		windows.MoveView(View(main, "B"), other);

		Assert.Equal(["A"], Names(main));
		Assert.DoesNotContain(chart, main.Widgets);
		Assert.Equal([WorkspaceConstants.DefaultViewName, "B"], Names(other));
		var moved = View(other, "B");
		Assert.Same(moved, other.SelectedView);
		Assert.Same(chart, other.Find(chart.Id));
		Assert.Same(content, chart.Content);
		Assert.Same(other, chart.Workspace);
		Assert.Equal(WidgetDock.Center, chart.Dock);
		Assert.Equal([chart.Id], CenterLayoutHelpers.GetWidgetIds(moved.Root));
	}

	[Fact]
	public void A_view_moved_to_another_window_is_saved_in_that_window()
	{
		var windows = CreateWindows();
		var main = windows.Main;
		main.AddView();
		main.SelectedView.Name = "Charts";
		var chart = SpawnChart(main, "Charts");
		var other = windows.Open();

		windows.MoveView(View(main, "Charts"), other.Workspace);

		var mainLayout = new LayoutDto();
		main.FillLayout(mainLayout);
		windows.FillLayout(mainLayout);
		Assert.DoesNotContain(mainLayout.Views, x => x.Name == "Charts");
		Assert.DoesNotContain(mainLayout.Widgets, x => x.Id == chart.Id);
		var saved = Assert.Single(mainLayout.Windows);
		Assert.Equal(other.Id, saved.Id);
		var view = Assert.Single(saved.Views, x => x.Name == "Charts");
		Assert.Equal(view.Id, saved.SelectedViewId);
		Assert.Equal([chart.Id], CenterLayoutHelpers.GetWidgetIds(view.Root));
		Assert.Contains(saved.Widgets, x => x.Id == chart.Id && x.Dock == WidgetDock.Center);
	}

	[Fact]
	public void A_moved_view_goes_to_the_tab_gap_it_was_dropped_on()
	{
		var windows = CreateWindows();
		var main = windows.Main;
		main.AddView();
		main.SelectedView.Name = "Moved";
		var other = windows.Open().Workspace;
		other.Views[0].Name = "X";
		other.AddView();
		other.SelectedView.Name = "Y";

		windows.MoveView(View(main, "Moved"), other, 1);

		Assert.Equal(["X", "Moved", "Y"], Names(other));
	}

	[Fact]
	public void A_widget_whose_id_is_taken_in_the_other_window_gets_a_new_one_in_the_tree_too()
	{
		var windows = CreateWindows();
		var main = windows.Main;
		main.AddView();
		main.SelectedView.Name = "Charts";
		var chart = SpawnChart(main, "Charts");
		var other = windows.Open().Workspace;
		var otherChart = SpawnChart(other, other.Views[0].Name);
		Assert.Equal(otherChart.Id, chart.Id);

		windows.MoveView(View(main, "Charts"), other);

		Assert.NotEqual(otherChart.Id, chart.Id);
		Assert.Same(chart, other.Find(chart.Id));
		Assert.Same(otherChart, other.Find(otherChart.Id));
		Assert.Equal([chart.Id], CenterLayoutHelpers.GetWidgetIds(View(other, "Charts").Root));
	}

	[Fact]
	public void Moving_the_only_view_out_of_the_main_window_leaves_an_empty_view_there()
	{
		var windows = CreateWindows();
		var main = windows.Main;
		var only = main.Views[0];
		SpawnChart(main, only.Name);
		var other = windows.Open().Workspace;

		windows.MoveView(only, other);

		var left = Assert.Single(main.Views);
		Assert.NotSame(only, left);
		Assert.Null(left.Root);
		Assert.Same(left, main.SelectedView);
	}

	[Fact]
	public void An_extra_window_left_with_nothing_after_its_last_view_moves_out_is_closed()
	{
		var windows = CreateWindows();
		var window = windows.Open();
		SpawnChart(window.Workspace, window.Workspace.Views[0].Name);

		windows.MoveView(window.Workspace.Views[0], windows.Main);

		Assert.DoesNotContain(window, windows.Windows);
		Assert.Single(windows.Main.Widgets, x => x.Tool == WorkspaceTool.Chart);
	}

	[Fact]
	public void An_extra_window_with_a_docked_widget_stays_open_when_its_last_view_moves_out()
	{
		var windows = CreateWindows();
		var window = windows.Open();
		window.Workspace.Spawn(WorkspaceTool.Watchlist);
		SpawnChart(window.Workspace, window.Workspace.Views[0].Name);

		windows.MoveView(window.Workspace.Views[0], windows.Main);

		Assert.Contains(window, windows.Windows);
		Assert.Null(Assert.Single(window.Workspace.Views).Root);
	}

	[Fact]
	public void Detaching_a_view_opens_a_new_window_holding_only_that_view()
	{
		var windows = CreateWindows();
		var main = windows.Main;
		main.Views[0].Name = "A";
		main.AddView();
		main.SelectedView.Name = "B";
		var chart = SpawnChart(main, "B");

		windows.DetachView(View(main, "B"), 300, 200);

		Assert.Equal(["A"], Names(main));
		var window = Assert.Single(windows.Windows);
		Assert.Equal((300d, 200d), (window.X!.Value, window.Y!.Value));
		var view = Assert.Single(window.Workspace.Views);
		Assert.Equal("B", view.Name);
		Assert.Same(view, window.Workspace.SelectedView);
		Assert.Same(chart, window.Workspace.Find(chart.Id));
		Assert.Equal([chart.Id], CenterLayoutHelpers.GetWidgetIds(view.Root));

		var layout = new LayoutDto();
		windows.FillLayout(layout);
		Assert.Equal(["B"], Assert.Single(layout.Windows).Views.Select(x => x.Name));
	}

	[Fact]
	public void Detaching_the_only_view_of_an_extra_window_replaces_that_window()
	{
		var windows = CreateWindows();
		var first = windows.Open();
		var chart = SpawnChart(first.Workspace, first.Workspace.Views[0].Name);

		windows.DetachView(first.Workspace.Views[0]);

		var window = Assert.Single(windows.Windows);
		Assert.NotSame(first, window);
		Assert.Same(chart, window.Workspace.Find(chart.Id));
	}

	[Fact]
	public void A_view_can_move_to_every_other_window_main_window_first()
	{
		var windows = CreateWindows();
		var second = windows.Open();
		var third = windows.Open();

		var targets = second.Workspace.GetMoveTargets();

		Assert.Equal(
			[(WorkspaceConstants.MainWindowName, windows.Main), (third.Name, third.Workspace)],
			targets.Select(x => (x.Name, x.Workspace))
		);
	}

	[Fact]
	public void A_fullscreen_widget_moved_away_with_its_view_is_no_longer_fullscreen_here()
	{
		var windows = CreateWindows();
		var main = windows.Main;
		main.AddView();
		main.SelectedView.Name = "Charts";
		var chart = SpawnChart(main, "Charts");
		main.CurrentWidget = chart;
		main.ToggleFullscreen(chart);
		var other = windows.Open().Workspace;

		windows.MoveView(View(main, "Charts"), other);

		Assert.Null(main.FullscreenWidget);
		Assert.Null(main.CurrentWidget);
		Assert.False(chart.IsFullscreen);
	}

	[Fact]
	public void Closing_the_main_window_alone_hands_it_the_first_extra_windows_workspace()
	{
		var windows = CreateWindows();
		var oldMain = windows.Main;
		var discarded = SpawnChart(oldMain, oldMain.Views[0].Name);
		var second = windows.Open();
		second.Workspace.Views[0].Name = "Second";
		var kept = SpawnChart(second.Workspace, "Second");
		var third = windows.Open();
		var changed = 0;
		windows.MainChanged += () => changed++;

		var taken = windows.ReplaceMain();

		Assert.Same(second, taken);
		Assert.Same(second.Workspace, windows.Main);
		Assert.Equal(1, changed);
		Assert.Equal([third], windows.Windows);
		Assert.DoesNotContain(oldMain, windows.Workspaces);
		Assert.Same(kept, windows.Main.Find(kept.Id));
		Assert.DoesNotContain(discarded, windows.Workspaces.SelectMany(w => w.Widgets));

		var layout = new LayoutDto();
		windows.Main.FillLayout(layout);
		windows.FillLayout(layout);
		Assert.Equal(["Second"], layout.Views.Select(v => v.Name));
		Assert.Equal([third.Id], layout.Windows.Select(w => w.Id));
	}

	[Fact]
	public void Moves_after_the_main_window_was_handed_over_still_reach_every_window()
	{
		var windows = CreateWindows();
		var second = windows.Open();
		var third = windows.Open();
		windows.ReplaceMain();

		Assert.Equal(
			[(WorkspaceConstants.MainWindowName, second.Workspace)],
			third.Workspace.GetMoveTargets().Select(t => (t.Name, t.Workspace))
		);
	}

	[Fact]
	public void The_main_window_has_no_extra_window_to_hand_over_to_when_it_is_alone()
	{
		var windows = CreateWindows();
		var main = windows.Main;

		Assert.Null(windows.ReplaceMain());
		Assert.Same(main, windows.Main);
	}
}
