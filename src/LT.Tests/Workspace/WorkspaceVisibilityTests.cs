using LT.App.Workspace;
using LT.Contracts.Dtos.Settings;
using LT.Contracts.Enums;

namespace LT.Tests.Workspace;

/// <summary>
/// The workspace tells each widget view model whether it is on screen, so a hidden one can give
/// its market data lines back: a center widget only in the shown view, a side or bottom widget
/// only while expanded, a floating one unless its window is minimized, nothing of a minimized
/// window but its floating widgets. Moving a widget within the window doesn't hide it.
/// </summary>
public class WorkspaceVisibilityTests
{
	private sealed class FakeWidget : IWidgetVisibility
	{
		public bool IsShown { get; private set; } = true;
		public List<bool> Calls { get; } = [];

		public void SetShown(bool isShown)
		{
			IsShown = isShown;
			Calls.Add(isShown);
		}
	}

	private static WorkspaceViewModel CreateWorkspace() =>
		new(_ => new FakeWidget(), new WorkspaceLayoutDto());

	private static FakeWidget Fake(WidgetItem widget) => (FakeWidget)widget.Content;

	[Fact]
	public void A_center_widget_is_hidden_while_another_view_is_shown()
	{
		var workspace = CreateWorkspace();
		var chart = workspace.Spawn(WorkspaceTool.Chart);
		var main = workspace.SelectedView;
		Assert.True(Fake(chart).IsShown);

		workspace.AddView();
		Assert.False(Fake(chart).IsShown);
		Assert.False(chart.IsShown);

		workspace.SelectView(main);
		Assert.True(Fake(chart).IsShown);
	}

	[Theory]
	[InlineData(WorkspaceTool.OrderTicket)]
	[InlineData(WorkspaceTool.Watchlist)]
	[InlineData(WorkspaceTool.News)]
	public void A_side_or_bottom_widget_is_shown_only_while_expanded(WorkspaceTool tool)
	{
		var workspace = CreateWorkspace();
		var widget = workspace.Spawn(tool);
		Assert.True(widget.IsSide);
		Assert.True(widget.IsActive);
		Assert.True(Fake(widget).IsShown);

		workspace.Collapse(widget);
		Assert.False(Fake(widget).IsShown);

		workspace.ToggleSide(widget);
		Assert.True(Fake(widget).IsShown);
	}

	[Fact]
	public void Expanding_another_widget_on_the_same_side_hides_the_first()
	{
		var workspace = CreateWorkspace();
		var ticket = workspace.Spawn(WorkspaceTool.OrderTicket);
		var depth = workspace.Spawn(WorkspaceTool.Depth);
		Assert.Equal(WidgetDock.Right, ticket.Dock);
		Assert.Equal(WidgetDock.Right, depth.Dock);

		Assert.False(Fake(ticket).IsShown);
		Assert.True(Fake(depth).IsShown);
	}

	[Fact]
	public void A_collapsed_side_widget_made_fullscreen_is_shown()
	{
		var workspace = CreateWorkspace();
		var ticket = workspace.Spawn(WorkspaceTool.OrderTicket);
		workspace.Collapse(ticket);

		workspace.ToggleFullscreen(ticket);

		Assert.True(Fake(ticket).IsShown);
	}

	[Fact]
	public void A_minimized_window_hides_its_center_and_side_widgets_but_not_floating_ones()
	{
		var workspace = CreateWorkspace();
		var chart = workspace.Spawn(WorkspaceTool.Chart);
		var ticket = workspace.Spawn(WorkspaceTool.OrderTicket);
		var floating = workspace.Spawn(WorkspaceTool.Watchlist);
		workspace.Drop(floating, WidgetDock.Floating);
		Assert.True(Fake(ticket).IsShown);
		Assert.True(Fake(floating).IsShown);

		workspace.IsWindowMinimized = true;

		Assert.False(Fake(chart).IsShown);
		Assert.False(Fake(ticket).IsShown);
		Assert.True(Fake(floating).IsShown);

		workspace.IsWindowMinimized = false;

		Assert.True(Fake(chart).IsShown);
		Assert.True(Fake(ticket).IsShown);
	}

	[Fact]
	public void A_floating_widget_is_hidden_while_its_window_is_minimized()
	{
		var workspace = CreateWorkspace();
		var chain = workspace.Spawn(WorkspaceTool.OptionChain);
		workspace.Drop(chain, WidgetDock.Floating);
		Assert.True(Fake(chain).IsShown);

		chain.IsFloatMinimized = true;
		Assert.False(Fake(chain).IsShown);

		chain.IsFloatMinimized = false;
		Assert.True(Fake(chain).IsShown);
	}

	[Fact]
	public void Moving_a_shown_widget_within_the_center_never_hides_it()
	{
		var workspace = CreateWorkspace();
		var chart = workspace.Spawn(WorkspaceTool.Chart);
		var other = workspace.Spawn(WorkspaceTool.Positions);
		workspace.Drop(other, WidgetDock.Center);

		workspace.Drop(chart, WidgetDock.Center, targetId: other.Id, edge: DropEdge.Bottom);

		Assert.Empty(Fake(chart).Calls);
		Assert.Empty(Fake(other).Calls);
		Assert.True(Fake(chart).IsShown);
	}

	[Fact]
	public void Moving_a_shown_widget_between_docks_never_hides_it()
	{
		var workspace = CreateWorkspace();
		var ticket = workspace.Spawn(WorkspaceTool.OrderTicket);

		workspace.Drop(ticket, WidgetDock.Center);
		workspace.Drop(ticket, WidgetDock.Bottom);
		workspace.Drop(ticket, WidgetDock.Floating);
		workspace.Drop(ticket, WidgetDock.Left);

		Assert.Empty(Fake(ticket).Calls);
		Assert.True(Fake(ticket).IsShown);
	}

	[Fact]
	public void A_widget_moved_to_another_window_is_told_only_by_the_window_it_lands_in()
	{
		var from = CreateWorkspace();
		var to = CreateWorkspace();
		var ticket = from.Spawn(WorkspaceTool.OrderTicket);
		to.IsWindowMinimized = true;

		from.ReleaseWidget(ticket);
		Assert.Empty(Fake(ticket).Calls);
		to.AdoptWidget(ticket, WidgetDock.Right);

		Assert.Equal([false], Fake(ticket).Calls);
	}

	[Fact]
	public void A_center_widget_dropped_on_a_view_that_isnt_shown_is_hidden_until_it_is()
	{
		var workspace = CreateWorkspace();
		var main = workspace.SelectedView;
		workspace.AddView();
		var second = workspace.SelectedView;
		workspace.SelectView(main);
		var chart = workspace.Spawn(WorkspaceTool.Chart);

		// Dropped on another tab: that view is shown, the widget with it
		workspace.Drop(chart, WidgetDock.Center, viewId: second.Id);
		Assert.True(Fake(chart).IsShown);

		workspace.SelectView(main);
		Assert.False(Fake(chart).IsShown);
	}

	[Fact]
	public void A_view_moved_to_another_window_is_shown_there()
	{
		var from = CreateWorkspace();
		var to = CreateWorkspace();
		var chart = from.Spawn(WorkspaceTool.Chart);
		var view = from.SelectedView;
		to.IsWindowMinimized = true;

		var transfer = from.ReleaseView(view)!;
		to.AdoptView(transfer);
		Assert.False(Fake(chart).IsShown);

		to.IsWindowMinimized = false;
		Assert.True(Fake(chart).IsShown);
	}
}
