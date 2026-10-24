using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using LT.Contracts.Enums;

namespace LT.App.Workspace;

/// <summary>
/// Drag and drop docking: a widget is dragged by its frame header and dropped on a side strip
/// (at an icon position), on a center tile, or outside the main window to float. Positions
/// arrive in screen pixels because a drag can start in a floating window.
/// </summary>
public partial class WorkspaceView
{
	// A collapsed side accepts drops on its strip plus this thin edge, so it never covers the
	// center tiles next to it
	private const double CollapsedSideDropWidth = 24;

	// Strip icon geometry, matching Button.tool in App.axaml (36 px button + 4 px bottom margin)
	private const double StripIconSize = 36;
	private const double StripIconSlot = 40;

	private const double FloatBannerWidth = 320;
	private const double FloatBannerHeight = 40;
	private const double FloatingWindowPadding = 6;

	/// <summary>
	/// Where a drop lands: side drops carry the strip and icon <see cref="Index"/>, center drops the
	/// widget dropped on (<see cref="TargetId"/>) and which <see cref="Edge"/> of it.
	/// </summary>
	private readonly record struct DropTarget(
		WidgetDock Dock,
		int? Index,
		Rect Highlight,
		string Label,
		ItemsControl? Strip = null,
		string? TargetId = null,
		DropEdge Edge = DropEdge.Right
	);

	// Strip icon pushed down to open a gap where the dropped icon will go
	private Control? _gapContainer;

	private void AttachDrag(WidgetFrame frame)
	{
		frame.DragStarted += ShowDropTarget;
		frame.DragMoved += ShowDropTarget;
		frame.DragCancelled += _ => EndDragFeedback();
		frame.DragDropped += OnDragDropped;
	}

	private void ShowDropTarget(WidgetFrame frame, PixelPoint screen)
	{
		var target = GetDropTarget(frame, screen);
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
		ToolTip.SetServiceEnabled(this, true);
		SetStripGap(null, null);
		DropOverlay.IsVisible = false;
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
		_gapContainer?.Margin = new Thickness(0, StripIconSlot, 0, 0);
	}

	private void OnDragDropped(WidgetFrame frame, PixelPoint screen)
	{
		var target = GetDropTarget(frame, screen);
		EndDragFeedback();
		if (_viewModel is null || frame.Widget is not { } widget)
			return;

		if (target.Dock == WidgetDock.Floating)
		{
			// Put the window where the header was released, under the pointer
			var scaling = TopLevel.GetTopLevel(frame)?.RenderScaling ?? 1;
			var grab = frame.GrabOffset + new Point(FloatingWindowPadding, FloatingWindowPadding);
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
							target.Edge
						),
					DispatcherPriority.Background
				);
			source.Content = null;
			source.Close();
			return;
		}

		// The drop may re-host the frame; let the current pointer handling finish first
		Dispatcher.UIThread.Post(() =>
			_viewModel.Drop(widget, target.Dock, target.Index, target.TargetId, target.Edge)
		);
	}

	private DropTarget GetDropTarget(WidgetFrame frame, PixelPoint screen)
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

		// Over a center widget: the nearest edge decides beside / above / below it
		foreach (var tile in _viewModel.Widgets.Where(x => x.IsCenter))
		{
			if (
				!_frames.TryGetValue(tile.Id, out var tileFrame)
				|| tileFrame.TranslatePoint(default, this) is not { } origin
			)
				continue;
			var rect = new Rect(origin, tileFrame.Bounds.Size);
			if (!rect.Contains(point))
				continue;
			if (tileFrame == frame)
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

		return new DropTarget(WidgetDock.Center, null, CenterHost.Bounds, "Dock in center");
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
