using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using LT.App.Controls;
using LT.Contracts.Helpers;

namespace LT.App.Workspace;

/// <summary>
/// Moving and closing views (center tabs). A tab dragged along its strip goes to the gap nearest
/// the pointer; dropped on another workspace window it moves there (at the gap, when dropped on
/// that window's tabs, else after them); dropped outside every window it opens in a new window of
/// its own. The tab's right-click menu does the same without dragging. ✕ asks before closing a
/// view with its widgets. Nothing is re-hosted across windows: the other window builds frames of
/// its own for the moved widgets.
/// </summary>
public partial class WorkspaceView
{
	// How far above and below the tab strip a dragged tab still counts as on it
	private const double TabStripDropReach = 16;
	private const double TabInsertBarWidth = 4;

	// Where a detached view's window opens, relative to the pointer (DIPs): its title under it
	private const double DetachOffsetX = 80;
	private const double DetachOffsetY = 16;

	/// <summary>Every workspace on screen: a dragged tab can be dropped on any of them.</summary>
	private static readonly List<WorkspaceView> Attached = [];

	/// <summary>
	/// Where a dragged tab lands: a workspace (<see cref="Host"/>) at tab gap <see cref="Slot"/>
	/// (null: after its tabs), a new window (<see cref="Detach"/>), or nowhere (both unset: released
	/// over its own window off the tabs).
	/// </summary>
	private readonly record struct ViewDropTarget(WorkspaceView? Host, int? Slot, bool Detach);

	// Tabs already given a drag (a tab can be attached to the visual tree again)
	private readonly ConditionalWeakTable<Border, DragGesture> _tabDrags = [];

	// The workspace showing where the dragged tab will land
	private WorkspaceView? _viewDropFeedback;

	private void OnViewTabAttached(object? sender, VisualTreeAttachmentEventArgs e)
	{
		if (sender is not Border tab || _tabDrags.TryGetValue(tab, out _))
			return;
		CenterViewItem? View() => tab.DataContext as CenterViewItem;
		// Not while its name is being edited, nor under a fullscreen widget
		var drag = new DragGesture(
			tab,
			RoutingStrategies.Bubble,
			() => View() is { IsRenaming: false } && _viewModel?.FullscreenWidget is null
		);
		drag.Started += screen => ShowViewDropTarget(View(), screen);
		drag.Moved += screen => ShowViewDropTarget(View(), screen);
		drag.Cancelled += EndViewDragFeedback;
		drag.Dropped += screen => OnViewTabDropped(View(), tab, screen);
		_tabDrags.Add(tab, drag);
		tab.AddHandler(PointerPressedEvent, OnViewTabPressed);
	}

	// ✕ on a tab: asks first, the view's widgets close with it
	private async void OnViewCloseClick(object? sender, RoutedEventArgs e)
	{
		if (
			sender is not Control { DataContext: CenterViewItem view }
			|| TopLevel.GetTopLevel(this) is not Window owner
		)
			return;
		var confirmed = await ConfirmDialog.ShowAsync(
			owner,
			"Close view",
			$"Close the view “{view.Name}”?",
			CenterViewHelpers.GetCloseDetail(view.WidgetCount),
			"Close view"
		);
		if (confirmed)
			view.CloseCommand.Execute(null);
	}

	// Right-click: "Move to window" lists the other windows as they are now
	private void OnViewTabMenuOpening(object? sender, System.ComponentModel.CancelEventArgs e)
	{
		if (
			sender is not ContextMenu { DataContext: CenterViewItem view } menu
			|| menu.Items.OfType<MenuItem>().FirstOrDefault(x => x.Classes.Contains("moveToWindow"))
				is not { } moveTo
		)
			return;
		var targets = view.GetMoveTargets();
		moveTo.Items.Clear();
		foreach (var target in targets)
			moveTo.Items.Add(
				new MenuItem
				{
					Header = target.Name,
					Command = view.MoveToCommand,
					CommandParameter = target.Workspace,
				}
			);
		moveTo.IsEnabled = targets.Count > 0;
	}

	private void ShowViewDropTarget(CenterViewItem? view, PixelPoint screen)
	{
		if (view is null)
			return;
		var target = GetViewDropTarget(screen);
		var host = target.Detach ? this : target.Host;
		if (_viewDropFeedback != host)
			_viewDropFeedback?.HideViewDropFeedback();
		_viewDropFeedback = host;
		if (target.Detach)
			ShowFeedback(
				new Rect(
					(Bounds.Width - FloatBannerWidth) / 2,
					8,
					FloatBannerWidth,
					FloatBannerHeight
				),
				$"Release to open “{view.Name}” in a new window"
			);
		else if (target.Host is { } other)
			other.ShowViewDropFeedback(view, target.Slot);
	}

	private void EndViewDragFeedback()
	{
		_viewDropFeedback?.HideViewDropFeedback();
		_viewDropFeedback = null;
	}

	private void OnViewTabDropped(CenterViewItem? view, Visual handle, PixelPoint screen)
	{
		var target = GetViewDropTarget(screen);
		EndViewDragFeedback();
		if (_viewModel is not { } workspace || view is null)
			return;
		if (target.Detach)
		{
			var scaling = TopLevel.GetTopLevel(handle)?.RenderScaling ?? 1;
			var x = screen.X - DetachOffsetX * scaling;
			var y = screen.Y - DetachOffsetY * scaling;
			// The move rebuilds the tabs, the dragged one included: after this release is handled
			Dispatcher.UIThread.Post(() => workspace.DetachView(view, x, y));
			return;
		}
		if (target.Host is not { _viewModel: { } to } host)
			return;
		Dispatcher.UIThread.Post(() =>
		{
			workspace.MoveViewTo(view, to, target.Slot);
			if (host != this)
				(TopLevel.GetTopLevel(host) as Window)?.Activate();
		});
	}

	/// <summary>
	/// Over this window: on its tabs, the gap there; elsewhere nothing. Over another workspace
	/// window: that window, at the gap when on its tabs. Over no window: a new one.
	/// </summary>
	private ViewDropTarget GetViewDropTarget(PixelPoint screen)
	{
		foreach (var host in Attached.Where(x => x != this).Prepend(this))
		{
			if (host._viewModel is null || !host.IsOverWindow(screen))
				continue;
			var slot = host.GetTabSlot(screen);
			return host == this && slot is null
				? new ViewDropTarget(null, null, false)
				: new ViewDropTarget(host, slot, false);
		}
		return new ViewDropTarget(null, null, true);
	}

	private bool IsOverWindow(PixelPoint screen)
	{
		if (
			TopLevel.GetTopLevel(this) is not Window { IsVisible: true } window
			|| window.WindowState == WindowState.Minimized
		)
			return false;
		var point = window.PointToClient(screen);
		return new Rect(window.ClientSize).Contains(point);
	}

	/// <summary>The tab gap nearest the pointer (0 = before the first tab); null off the tab strip.</summary>
	private int? GetTabSlot(PixelPoint screen)
	{
		if (
			!ViewTabStrip.IsEffectivelyVisible
			|| ViewTabStrip.TranslatePoint(default, this) is not { } origin
		)
			return null;
		var point = this.PointToClient(screen);
		var strip = new Rect(origin, ViewTabStrip.Bounds.Size).Inflate(
			new Thickness(0, TabStripDropReach)
		);
		if (!strip.Contains(point))
			return null;
		var slot = 0;
		foreach (var rect in GetTabRects())
			if (rect.Center.X < point.X)
				slot++;
		return slot;
	}

	// Each tab's bounds in this view's coordinates, in order
	private List<Rect> GetTabRects()
	{
		var rects = new List<Rect>();
		for (var i = 0; i < ViewTabs.ItemCount; i++)
			if (
				ViewTabs.ContainerFromIndex(i) is { } container
				&& container.TranslatePoint(default, this) is { } origin
			)
				rects.Add(new Rect(origin, container.Bounds.Size));
		return rects;
	}

	// On this workspace: a bar in the tab gap, or (dropped off the tabs of another window) the
	// whole center
	private void ShowViewDropFeedback(CenterViewItem view, int? slot)
	{
		var tabs = GetTabRects();
		if (slot is { } i && tabs.Count > 0)
		{
			var x =
				i <= 0 ? tabs[0].Left
				: i >= tabs.Count ? tabs[^1].Right
				: (tabs[i - 1].Right + tabs[i].Left) / 2;
			var tab = tabs[Math.Clamp(i, 0, tabs.Count - 1)];
			ShowFeedback(
				new Rect(x - TabInsertBarWidth / 2, tab.Top, TabInsertBarWidth, tab.Height),
				string.Empty
			);
			return;
		}
		var center = CenterHost.TranslatePoint(default, this) is { } hostOrigin
			? new Rect(hostOrigin, CenterHost.Bounds.Size)
			: CenterHost.Bounds;
		ShowFeedback(center, $"Move “{view.Name}” to this window");
	}

	private void ShowFeedback(Rect highlight, string label)
	{
		Canvas.SetLeft(DropHighlight, highlight.X);
		Canvas.SetTop(DropHighlight, highlight.Y);
		DropHighlight.Width = highlight.Width;
		DropHighlight.Height = highlight.Height;
		DropLabel.Text = label;
		DropLabel.IsVisible = label.Length > 0;
		DropOverlay.IsVisible = true;
	}

	private void HideViewDropFeedback() => DropOverlay.IsVisible = false;
}
