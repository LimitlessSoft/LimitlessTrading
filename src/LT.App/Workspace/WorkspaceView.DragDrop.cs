using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using LT.Contracts.Constants;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;

namespace LT.App.Workspace;

/// <summary>
/// Drag and drop docking: a widget is dragged by its frame header (or, docked on a strip, by its
/// strip icon) and dropped on a strip (left, right or bottom, at an icon position), on a center
/// tile, on a view tab, or outside the main window to float. Held over a view tab for a moment,
/// the drag shows that view so the widget can be dropped into it. Positions arrive in screen
/// pixels because a drag can start in a floating window.
/// </summary>
public partial class WorkspaceView
{
	// A collapsed side (or bottom) accepts drops on its strip plus this thin edge, so it never
	// covers the center tiles next to it
	private const double CollapsedSideDropWidth = 24;

	// Strip icon geometry, matching Button.tool in App.axaml (36 px button + 4 px bottom margin)
	private const double StripIconSize = 36;
	private const double StripIconSlot = 40;

	private const double FloatBannerWidth = 320;
	private const double FloatBannerHeight = 40;

	/// <summary>
	/// Where a drop lands: side drops carry the strip and icon <see cref="Index"/>, center drops the
	/// widget dropped on (<see cref="TargetId"/>) and which <see cref="Edge"/> of it, or the view
	/// whose tab it was dropped on (<see cref="View"/>).
	/// </summary>
	private readonly record struct DropTarget(
		WidgetDock Dock,
		int? Index,
		Rect Highlight,
		string Label,
		ItemsControl? Strip = null,
		string? TargetId = null,
		DropEdge Edge = DropEdge.Right,
		CenterViewItem? View = null
	);

	// The view tab a drag is held over, and the timer that shows its view
	private CenterViewItem? _hoverView;
	private DispatcherTimer? _hoverTimer;

	// Strip icon pushed down to open a gap where the dropped icon will go
	private Control? _gapContainer;

	// Strip icons already given a drag (an icon can be attached to the visual tree again)
	private readonly ConditionalWeakTable<Button, DragGesture> _iconDrags = [];

	private void AttachDrag(WidgetFrame frame)
	{
		frame.DragStarted += (_, screen) => ShowDropTarget(frame.Widget, screen);
		frame.DragMoved += (_, screen) => ShowDropTarget(frame.Widget, screen);
		frame.DragCancelled += _ => EndDragFeedback();
		frame.DragDropped += (_, screen) =>
			OnDragDropped(frame.Widget, frame.GrabOffset, frame, screen);
	}

	// A strip icon drags its widget like the header does; a click that doesn't move past the
	// threshold still toggles it, a drag doesn't. The press is seen before the button handles it.
	private void OnStripIconAttached(object? sender, VisualTreeAttachmentEventArgs e)
	{
		if (sender is not Button icon || _iconDrags.TryGetValue(icon, out _))
			return;
		WidgetItem? Widget() => icon.DataContext as WidgetItem;
		var drag = new DragGesture(
			icon,
			RoutingStrategies.Tunnel,
			() => Widget()?.IsFullscreen != true
		);
		drag.Started += screen => ShowDropTarget(Widget(), screen);
		drag.Moved += screen => ShowDropTarget(Widget(), screen);
		drag.Cancelled += EndDragFeedback;
		drag.Dropped += screen => OnDragDropped(Widget(), drag.GrabOffset, icon, screen);
		_iconDrags.Add(icon, drag);
	}

	private void ShowDropTarget(WidgetItem? widget, PixelPoint screen)
	{
		if (widget is null)
			return;
		var target = GetDropTarget(widget, screen);
		SetHoverView(target.View);
		// Strip icon tooltips would pop up over the drop slot
		ToolTip.SetServiceEnabled(this, false);
		SetStripGap(target.Strip, target.Index);
		Canvas.SetLeft(DropHighlight, target.Highlight.X);
		Canvas.SetTop(DropHighlight, target.Highlight.Y);
		DropHighlight.Width = target.Highlight.Width;
		DropHighlight.Height = target.Highlight.Height;
		DropLabel.Text = target.Label;
		DropLabel.IsVisible = target.Label.Length > 0;
		DropOverlay.IsVisible = true;
	}

	private void EndDragFeedback()
	{
		SetHoverView(null);
		ToolTip.SetServiceEnabled(this, true);
		SetStripGap(null, null);
		DropOverlay.IsVisible = false;
	}

	/// <summary>
	/// Holding a drag over the tab of another view shows that view after
	/// <see cref="WorkspaceConstants.ViewTabHoverSwitchMs"/>. Only frame visibility changes, so the
	/// dragged frame keeps its pointer capture even when it was in the view being hidden.
	/// </summary>
	private void SetHoverView(CenterViewItem? view)
	{
		if (view == _hoverView)
			return;
		_hoverView = view;
		_hoverTimer?.Stop();
		if (view is null || view == _viewModel?.SelectedView)
			return;
		_hoverTimer ??= CreateHoverTimer();
		_hoverTimer.Start();
	}

	private DispatcherTimer CreateHoverTimer()
	{
		var timer = new DispatcherTimer
		{
			Interval = TimeSpan.FromMilliseconds(WorkspaceConstants.ViewTabHoverSwitchMs),
		};
		timer.Tick += (_, _) =>
		{
			timer.Stop();
			if (_hoverView != null)
				_viewModel?.SelectView(_hoverView);
		};
		return timer;
	}

	/// <summary>Opens a one-icon gap in a strip at <paramref name="index"/>, closing any other.</summary>
	private void SetStripGap(ItemsControl? strip, int? index)
	{
		var container =
			strip != null && index is { } i && i < strip.ItemCount
				? strip.ContainerFromIndex(i)
				: null;
		if (container == _gapContainer)
			return;
		_gapContainer?.Margin = default;
		_gapContainer = container;
		_gapContainer?.Margin =
			strip == BottomIcons
				? new Thickness(StripIconSlot, 0, 0, 0)
				: new Thickness(0, StripIconSlot, 0, 0);
	}

	/// <param name="grabOffset">Where the handle was grabbed, relative to the floating window's content.</param>
	/// <param name="handle">The dragged frame or strip icon, for the screen scaling.</param>
	private void OnDragDropped(
		WidgetItem? widget,
		Point grabOffset,
		Visual handle,
		PixelPoint screen
	)
	{
		if (_viewModel is null || widget is null)
		{
			EndDragFeedback();
			return;
		}
		var target = GetDropTarget(widget, screen);
		EndDragFeedback();

		if (target.Dock == WidgetDock.Floating)
		{
			// Put the window where the handle was released, under the pointer
			var scaling = TopLevel.GetTopLevel(handle)?.RenderScaling ?? 1;
			var grab = grabOffset + new Point(FloatingPadding.Left, FloatingPadding.Top);
			var position =
				screen - new PixelPoint((int)(grab.X * scaling), (int)(grab.Y * scaling));
			if (_windows.TryGetValue(widget.Id, out var window))
			{
				window.Position = position;
				return;
			}
			widget.FloatX = position.X;
			widget.FloatY = position.Y;
		}

		// Leaving a floating window: close it first and dock only once it is gone. Moving a control
		// between windows while the old one still has it queued for layout crashes Avalonia.
		if (target.Dock != WidgetDock.Floating && _windows.Remove(widget.Id, out var source))
		{
			StoreBounds(widget, source);
			source.Closed += (_, _) =>
				Dispatcher.UIThread.Post(
					() =>
						_viewModel.Drop(
							widget,
							target.Dock,
							target.Index,
							target.TargetId,
							target.Edge,
							target.View?.Id
						),
					DispatcherPriority.Background
				);
			source.Content = null;
			source.Close();
			return;
		}

		// The drop may re-host the frame; let the current pointer handling finish first
		Dispatcher.UIThread.Post(() =>
			_viewModel.Drop(
				widget,
				target.Dock,
				target.Index,
				target.TargetId,
				target.Edge,
				target.View?.Id
			)
		);
	}

	private DropTarget GetDropTarget(WidgetItem widget, PixelPoint screen)
	{
		var point = this.PointToClient(screen);
		var size = Bounds.Size;
		if (point.X < 0 || point.Y < 0 || point.X > size.Width || point.Y > size.Height)
			return new DropTarget(
				WidgetDock.Floating,
				null,
				new Rect(
					(size.Width - FloatBannerWidth) / 2,
					8,
					FloatBannerWidth,
					FloatBannerHeight
				),
				"Release to float in its own window"
			);

		var leftWidth = _viewModel!.LeftActive != null ? LeftHost.Bounds.Width : 0;
		if (point.X < LeftStrip.Bounds.Right + Math.Max(leftWidth, CollapsedSideDropWidth))
			return GetStripTarget(WidgetDock.Left, LeftStrip, LeftIcons, point);

		var rightWidth = _viewModel.RightActive != null ? RightHost.Bounds.Width : 0;
		if (point.X > RightStrip.Bounds.Left - Math.Max(rightWidth, CollapsedSideDropWidth))
			return GetStripTarget(WidgetDock.Right, RightStrip, RightIcons, point);

		if (GetBottomTarget(point) is { } bottom)
			return bottom;

		if (GetViewTabTarget(point) is { } tab)
			return tab;

		// Over a center widget of the shown view: the nearest edge decides beside / above / below it
		foreach (
			var tile in CenterLayoutHelpers
				.GetWidgetIds(_viewModel.CenterRoot)
				.Select(_viewModel.Find)
				.OfType<WidgetItem>()
		)
		{
			if (
				!_frames.TryGetValue(tile.Id, out var tileFrame)
				|| tileFrame.TranslatePoint(default, this) is not { } origin
			)
				continue;
			var rect = new Rect(origin, tileFrame.Bounds.Size);
			if (!rect.Contains(point))
				continue;
			if (tile == widget)
				return new DropTarget(
					WidgetDock.Center,
					null,
					rect,
					"Keep here",
					TargetId: tile.Id
				);

			var x = (point.X - rect.X) / rect.Width;
			var y = (point.Y - rect.Y) / rect.Height;
			var edge = new[]
			{
				(Edge: DropEdge.Left, Distance: x),
				(Edge: DropEdge.Right, Distance: 1 - x),
				(Edge: DropEdge.Top, Distance: y),
				(Edge: DropEdge.Bottom, Distance: 1 - y),
			}
				.MinBy(e => e.Distance)
				.Edge;
			var (highlight, label) = edge switch
			{
				DropEdge.Left => (rect.WithWidth(rect.Width / 2), $"Left of {tile.Title}"),
				DropEdge.Right => (
					new Rect(rect.Center.X, rect.Y, rect.Width / 2, rect.Height),
					$"Right of {tile.Title}"
				),
				DropEdge.Top => (rect.WithHeight(rect.Height / 2), $"Above {tile.Title}"),
				_ => (
					new Rect(rect.X, rect.Center.Y, rect.Width, rect.Height / 2),
					$"Below {tile.Title}"
				),
			};
			return new DropTarget(WidgetDock.Center, null, highlight, label, null, tile.Id, edge);
		}

		// The center host's own bounds are relative to the center column, below the view tabs
		var center = CenterHost.TranslatePoint(default, this) is { } hostOrigin
			? new Rect(hostOrigin, CenterHost.Bounds.Size)
			: CenterHost.Bounds;
		return new DropTarget(WidgetDock.Center, null, center, "Dock in center");
	}

	/// <summary>
	/// Over a view tab: dropping puts the widget in that view (right of everything) and shows it.
	/// Null when the pointer isn't on a tab.
	/// </summary>
	private DropTarget? GetViewTabTarget(Point point)
	{
		for (var i = 0; i < ViewTabs.ItemCount; i++)
		{
			if (
				ViewTabs.ContainerFromIndex(i) is not { } container
				|| container.TranslatePoint(default, this) is not { } origin
				|| container.DataContext is not CenterViewItem view
			)
				continue;
			var rect = new Rect(origin, container.Bounds.Size);
			if (rect.Contains(point))
				return new DropTarget(WidgetDock.Center, null, rect, string.Empty, View: view);
		}
		return null;
	}

	/// <summary>
	/// Bottom drop: over the open bottom panel or its strip, or (collapsed / no bottom widgets yet)
	/// a thin band along the center column's bottom edge. Null when the pointer is elsewhere.
	/// </summary>
	private DropTarget? GetBottomTarget(Point point)
	{
		if (CenterColumn.TranslatePoint(default, this) is not { } origin)
			return null;
		var column = new Rect(origin, CenterColumn.Bounds.Size);
		var strip = BottomStrip.IsVisible ? BottomStrip.Bounds.Height : 0;
		var panel = _viewModel!.BottomActive != null ? BottomHost.Bounds.Height : 0;
		var zoneTop = column.Bottom - strip - Math.Max(panel, CollapsedSideDropWidth);
		if (point.X < column.Left || point.X > column.Right || point.Y < zoneTop)
			return null;
		if (!BottomStrip.IsVisible)
			return new DropTarget(
				WidgetDock.Bottom,
				null,
				new Rect(
					column.Left,
					column.Bottom - CollapsedSideDropWidth,
					column.Width,
					CollapsedSideDropWidth
				),
				"Dock at the bottom"
			);

		// The icon slot nearest the pointer, along the strip's row
		var left = BottomIcons.TranslatePoint(default, this)?.X ?? column.Left;
		var index = Math.Clamp(
			(int)Math.Round((point.X - left) / StripIconSlot),
			0,
			BottomIcons.ItemCount
		);
		var stripTop = column.Bottom - strip;
		var slot = new Rect(
			left + index * StripIconSlot,
			stripTop + (strip - StripIconSize) / 2,
			StripIconSize,
			StripIconSize
		);
		return new DropTarget(WidgetDock.Bottom, index, slot, string.Empty, BottomIcons);
	}

	/// <summary>
	/// Side drop: the icon slot nearest the pointer. Slots are computed from the strip's resting
	/// layout (not the gap-shifted one) so the target doesn't jump while the gap opens.
	/// </summary>
	private DropTarget GetStripTarget(
		WidgetDock dock,
		Border strip,
		ItemsControl icons,
		Point point
	)
	{
		var top = icons.TranslatePoint(default, this)?.Y ?? strip.Bounds.Top;
		var index = Math.Clamp(
			(int)Math.Round((point.Y - top) / StripIconSlot),
			0,
			icons.ItemCount
		);
		var slot = new Rect(
			strip.Bounds.X + (strip.Bounds.Width - StripIconSize) / 2,
			top + index * StripIconSlot,
			StripIconSize,
			StripIconSize
		);
		return new DropTarget(dock, index, slot, string.Empty, icons);
	}
}
