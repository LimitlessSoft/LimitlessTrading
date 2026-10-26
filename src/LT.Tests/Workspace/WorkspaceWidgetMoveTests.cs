using LT.App.Stores;
using LT.App.Workspace;
using LT.Contracts.Dtos.Settings;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;
using LT.Domain.Managers;
using LT.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;

namespace LT.Tests.Workspace;

/// <summary>
/// A widget dragged from one workspace window and dropped on another: it leaves the first and
/// lands on the drop target of the second, view model and settings included.
/// </summary>
public class WorkspaceWidgetMoveTests
{
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

	private static WidgetItem SpawnAt(
		WorkspaceViewModel workspace,
		WorkspaceTool tool,
		WidgetDock dock
	)
	{
		var widget = workspace.Spawn(tool);
		workspace.Drop(widget, dock);
		return widget;
	}

	[Fact]
	public void A_center_widget_dropped_on_another_window_moves_there_beside_the_target()
	{
		var windows = CreateWindows();
		var main = windows.Main;
		var chart = SpawnAt(main, WorkspaceTool.Chart, WidgetDock.Center);
		var content = chart.Content;
		var other = windows.Open().Workspace;
		var watchlist = SpawnAt(other, WorkspaceTool.Watchlist, WidgetDock.Center);

		main.MoveWidgetTo(
			chart,
			other,
			WidgetDock.Center,
			targetId: watchlist.Id,
			edge: DropEdge.Left
		);

		Assert.DoesNotContain(chart, main.Widgets);
		Assert.Null(main.CenterRoot);
		Assert.Same(chart, other.Find(chart.Id));
		Assert.Same(other, chart.Workspace);
		Assert.Same(content, chart.Content);
		Assert.Equal(WidgetDock.Center, chart.Dock);
		Assert.Equal([chart.Id, watchlist.Id], CenterLayoutHelpers.GetWidgetIds(other.CenterRoot));
	}

	[Fact]
	public void A_side_widget_dropped_on_another_windows_strip_goes_to_that_icon_slot_and_opens()
	{
		var windows = CreateWindows();
		var main = windows.Main;
		var orders = SpawnAt(main, WorkspaceTool.Orders, WidgetDock.Left);
		Assert.Same(orders, main.LeftActive);
		var other = windows.Open().Workspace;
		var positions = SpawnAt(other, WorkspaceTool.Positions, WidgetDock.Right);

		windows.MoveWidget(orders, other, WidgetDock.Right, index: 0);

		Assert.Null(main.LeftActive);
		Assert.Empty(main.Left);
		Assert.Null(main.CurrentWidget);
		Assert.Equal([orders, positions], other.Right);
		Assert.Same(orders, other.RightActive);
		Assert.Same(orders, other.CurrentWidget);
	}

	[Fact]
	public void A_floating_widget_dropped_on_another_window_docks_there()
	{
		var windows = CreateWindows();
		var main = windows.Main;
		var news = SpawnAt(main, WorkspaceTool.News, WidgetDock.Floating);
		var other = windows.Open().Workspace;

		main.MoveWidgetTo(news, other, WidgetDock.Bottom);

		Assert.Empty(main.Floating);
		Assert.Equal([news], other.Bottom);
		Assert.Same(news, other.BottomActive);
	}

	[Fact]
	public void A_widget_dropped_on_another_windows_view_tab_goes_into_that_view()
	{
		var windows = CreateWindows();
		var main = windows.Main;
		var chart = SpawnAt(main, WorkspaceTool.Chart, WidgetDock.Center);
		var other = windows.Open().Workspace;
		var first = other.Views[0];
		other.AddView();

		main.MoveWidgetTo(chart, other, WidgetDock.Center, viewId: first.Id);

		Assert.Same(first, other.SelectedView);
		Assert.Equal([chart.Id], CenterLayoutHelpers.GetWidgetIds(first.Root));
	}

	[Fact]
	public void A_widget_whose_id_is_taken_in_the_other_window_gets_a_new_one()
	{
		var windows = CreateWindows();
		var main = windows.Main;
		var chart = SpawnAt(main, WorkspaceTool.Chart, WidgetDock.Center);
		var other = windows.Open().Workspace;
		var otherChart = SpawnAt(other, WorkspaceTool.Chart, WidgetDock.Center);
		Assert.Equal(otherChart.Id, chart.Id);

		main.MoveWidgetTo(chart, other, WidgetDock.Center);

		Assert.NotEqual(otherChart.Id, chart.Id);
		Assert.Same(chart, other.Find(chart.Id));
		Assert.Same(otherChart, other.Find(otherChart.Id));
		Assert.Equal([otherChart.Id, chart.Id], CenterLayoutHelpers.GetWidgetIds(other.CenterRoot));
	}

	[Fact]
	public void A_widget_moved_to_another_window_is_saved_in_that_window()
	{
		var windows = CreateWindows();
		var main = windows.Main;
		var chart = SpawnAt(main, WorkspaceTool.Chart, WidgetDock.Center);
		var other = windows.Open();

		main.MoveWidgetTo(chart, other.Workspace, WidgetDock.Left);

		var layout = new LayoutDto();
		main.FillLayout(layout);
		windows.FillLayout(layout);
		Assert.DoesNotContain(layout.Widgets, x => x.Id == chart.Id);
		var saved = Assert.Single(layout.Windows);
		Assert.Contains(saved.Widgets, x => x.Id == chart.Id && x.Dock == WidgetDock.Left);
		Assert.Equal(chart.Id, saved.LeftWidgetId);
	}

	[Fact]
	public void An_extra_window_left_with_nothing_after_its_last_widget_moves_out_is_closed()
	{
		var windows = CreateWindows();
		var window = windows.Open();
		var chart = SpawnAt(window.Workspace, WorkspaceTool.Chart, WidgetDock.Center);

		window.Workspace.MoveWidgetTo(chart, windows.Main, WidgetDock.Center);

		Assert.DoesNotContain(window, windows.Windows);
		Assert.Same(chart, windows.Main.Find(chart.Id));
	}

	[Fact]
	public void The_main_window_stays_when_its_last_widget_moves_out()
	{
		var windows = CreateWindows();
		var chart = SpawnAt(windows.Main, WorkspaceTool.Chart, WidgetDock.Center);
		var other = windows.Open();

		windows.Main.MoveWidgetTo(chart, other.Workspace, WidgetDock.Center);

		Assert.Contains(other, windows.Windows);
		Assert.Empty(windows.Main.Widgets);
	}

	[Fact]
	public void Moving_a_widget_to_its_own_window_is_a_plain_drop()
	{
		var windows = CreateWindows();
		var main = windows.Main;
		var chart = SpawnAt(main, WorkspaceTool.Chart, WidgetDock.Center);

		main.MoveWidgetTo(chart, main, WidgetDock.Right);

		Assert.Equal([chart], main.Right);
		Assert.Same(main, chart.Workspace);
		Assert.Single(windows.Workspaces);
	}
}
