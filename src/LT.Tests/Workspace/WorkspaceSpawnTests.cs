using LT.App.Stores;
using LT.App.Workspace;
using LT.Contracts.Dtos.Settings;
using LT.Contracts.Enums;
using LT.Domain.Managers;
using LT.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;

namespace LT.Tests.Workspace;

/// <summary>
/// Every widget can be open any number of times: menu → Widgets spawns a new instance each time,
/// each with its own view model and settings, saved and restored per instance; closing one
/// discards only it. Widget view models are stand-ins that keep a setting and know whether they
/// were disposed.
/// </summary>
public class WorkspaceSpawnTests
{
	private sealed class FakeWidget : IWidgetState, IDisposable
	{
		public string? Setting { get; set; }
		public bool IsDisposed { get; private set; }

		// Stands for any per-instance setting (symbol, watchlist, …)
		public void LoadState(WidgetPlacementDto placement) => Setting = placement.WatchlistId;

		public void SaveState(WidgetPlacementDto placement) => placement.WatchlistId = Setting;

		public void Dispose() => IsDisposed = true;
	}

	private static WorkspaceViewModel CreateWorkspace(WorkspaceLayoutDto? layout = null) =>
		new(_ => new FakeWidget(), layout ?? new WorkspaceLayoutDto());

	private static List<WidgetItem> Instances(WorkspaceViewModel workspace, WorkspaceTool tool) =>
		workspace.Widgets.Where(x => x.Tool == tool).ToList();

	public static TheoryData<WorkspaceTool> Tools => [.. Enum.GetValues<WorkspaceTool>()];

	[Theory]
	[MemberData(nameof(Tools))]
	public void Spawning_a_widget_twice_opens_two_instances_with_their_own_view_models(
		WorkspaceTool tool
	)
	{
		var workspace = CreateWorkspace();

		var first = workspace.Spawn(tool);
		var second = workspace.Spawn(tool);

		Assert.NotSame(first, second);
		Assert.NotEqual(first.Id, second.Id);
		Assert.NotSame(first.Content, second.Content);
		Assert.True(first.IsOpen);
		Assert.True(second.IsOpen);
		Assert.Equal(2, Instances(workspace, tool).Count);
	}

	[Fact]
	public void A_spawned_instance_becomes_the_current_widget()
	{
		var workspace = CreateWorkspace();
		workspace.Spawn(WorkspaceTool.Orders);

		var second = workspace.Spawn(WorkspaceTool.Orders);

		Assert.Same(second, workspace.CurrentWidget);
	}

	[Fact]
	public void Closing_one_instance_discards_only_that_one()
	{
		var workspace = CreateWorkspace();
		var first = workspace.Spawn(WorkspaceTool.Account);
		var second = workspace.Spawn(WorkspaceTool.Account);

		workspace.Close(first);

		Assert.True(((FakeWidget)first.Content).IsDisposed);
		Assert.False(((FakeWidget)second.Content).IsDisposed);
		Assert.Equal([second], Instances(workspace, WorkspaceTool.Account));
		Assert.True(second.IsOpen);
		Assert.Contains(second, workspace.Right);
		Assert.DoesNotContain(first, workspace.Right);
	}

	[Fact]
	public void Several_instances_of_a_widget_are_saved_and_restored_with_their_own_settings()
	{
		var workspace = CreateWorkspace();
		foreach (var tool in new[] { WorkspaceTool.Watchlist, WorkspaceTool.Orders })
		{
			((FakeWidget)workspace.Spawn(tool).Content).Setting = $"{tool} 1";
			((FakeWidget)workspace.Spawn(tool).Content).Setting = $"{tool} 2";
		}
		var orders = Instances(workspace, WorkspaceTool.Orders);
		workspace.Drop(orders[1], WidgetDock.Floating);
		var layout = new WorkspaceLayoutDto();
		workspace.FillLayout(layout);

		var restored = CreateWorkspace(layout);

		Assert.Equal(
			workspace.Widgets.Select(x => (x.Id, x.Tool, x.Dock)).OrderBy(x => x.Id),
			restored.Widgets.Select(x => (x.Id, x.Tool, x.Dock)).OrderBy(x => x.Id)
		);
		Assert.Equal(
			["Orders 1", "Orders 2"],
			Instances(restored, WorkspaceTool.Orders).Select(x => ((FakeWidget)x.Content).Setting)
		);
		Assert.Equal(
			["Watchlist 1", "Watchlist 2"],
			Instances(restored, WorkspaceTool.Watchlist)
				.Select(x => ((FakeWidget)x.Content).Setting)
		);
		Assert.Equal(WidgetDock.Floating, Instances(restored, WorkspaceTool.Orders)[1].Dock);
	}

	[Fact]
	public void A_saved_layout_with_two_instances_of_a_widget_keeps_both()
	{
		var workspace = CreateWorkspace(
			new WorkspaceLayoutDto
			{
				Widgets =
				[
					new WidgetPlacementDto
					{
						Id = "Positions",
						Tool = WorkspaceTool.Positions,
						Dock = WidgetDock.Left,
					},
					new WidgetPlacementDto
					{
						Id = "Positions-1a2b3c4d",
						Tool = WorkspaceTool.Positions,
						Dock = WidgetDock.Bottom,
					},
				],
			}
		);

		Assert.Equal(
			["Positions", "Positions-1a2b3c4d"],
			Instances(workspace, WorkspaceTool.Positions).Select(x => x.Id)
		);
		Assert.Single(workspace.Left);
		Assert.Single(workspace.Bottom);
	}

	[Fact]
	public void Showing_a_widget_reveals_an_open_instance_and_spawns_one_only_when_none_is_open()
	{
		var workspace = CreateWorkspace();

		var spawned = workspace.Show(WorkspaceTool.Orders);
		var shown = workspace.Show(WorkspaceTool.Orders);

		Assert.Same(spawned, shown);
		Assert.Single(Instances(workspace, WorkspaceTool.Orders));

		workspace.Close(spawned);
		var again = workspace.Show(WorkspaceTool.Orders);

		Assert.NotSame(spawned, again);
		Assert.Single(Instances(workspace, WorkspaceTool.Orders));
	}

	[Fact]
	public void Showing_a_widget_prefers_the_current_instance()
	{
		var workspace = CreateWorkspace();
		var first = workspace.Spawn(WorkspaceTool.Watchlist);
		workspace.Spawn(WorkspaceTool.Watchlist);
		workspace.CurrentWidget = first;

		Assert.Same(first, workspace.Show(WorkspaceTool.Watchlist));
	}

	[Fact]
	public void The_chart_hotkey_opens_a_new_chart_and_the_others_toggle_the_open_instance()
	{
		var workspace = CreateWorkspace();

		workspace.Toggle(WorkspaceTool.Chart);
		workspace.Toggle(WorkspaceTool.Chart);
		Assert.Equal(2, Instances(workspace, WorkspaceTool.Chart).Count);

		workspace.Toggle(WorkspaceTool.Orders);
		var orders = Assert.Single(Instances(workspace, WorkspaceTool.Orders));
		Assert.Same(orders, workspace.LeftActive);
		workspace.Toggle(WorkspaceTool.Orders);
		Assert.Null(workspace.LeftActive);
		Assert.Single(Instances(workspace, WorkspaceTool.Orders));
	}

	[Fact]
	public void Duplicating_a_view_copies_every_widget_in_it_with_its_settings()
	{
		var workspace = CreateWorkspace();
		var chart = workspace.Spawn(WorkspaceTool.Chart);
		var orders = workspace.Spawn(WorkspaceTool.Orders);
		((FakeWidget)orders.Content).Setting = "kept";
		workspace.Drop(orders, WidgetDock.Center, targetId: chart.Id);
		var source = workspace.SelectedView;

		workspace.DuplicateView(source);

		Assert.NotSame(source, workspace.SelectedView);
		Assert.Equal(2, Instances(workspace, WorkspaceTool.Orders).Count);
		var copy = Instances(workspace, WorkspaceTool.Orders).Single(x => x != orders);
		Assert.Equal("kept", ((FakeWidget)copy.Content).Setting);
		Assert.Same(workspace.SelectedView, workspace.FindView(copy.Id));
		Assert.Same(source, workspace.FindView(orders.Id));
	}

	[Fact]
	public void Every_window_opens_its_own_instance_and_keeps_it()
	{
		var windows = CreateWindows();
		var extra = windows.Open().Workspace;

		var main = windows.Main.Spawn(WorkspaceTool.Account);
		var other = extra.Spawn(WorkspaceTool.Account);

		Assert.Equal([main], Instances(windows.Main, WorkspaceTool.Account));
		Assert.Equal([other], Instances(extra, WorkspaceTool.Account));
		Assert.True(main.IsOpen);
		Assert.True(other.IsOpen);
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
			services.AddTransient(definition.ViewModelType, _ => new FakeWidget());
		return new WorkspaceWindows(
			new WidgetViewModelFactory(services.BuildServiceProvider()),
			settings,
			new AppearanceStore(settings)
		);
	}
}
