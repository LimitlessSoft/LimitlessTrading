using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using LT.Contracts.Helpers;

namespace LT.App.Workspace;

/// <summary>
/// Press and drag on a handle (a frame header, a strip icon): the press captures the pointer and
/// becomes a drag once it moves past <see cref="WorkspaceHelpers.IsBeyondDragThreshold"/>. A
/// release after a drag is handled before the handle sees it, so a button used as the handle
/// doesn't click; a release without a drag is left to the handle (the button's click).
/// </summary>
public sealed class DragGesture
{
	private readonly Control _handle;
	private readonly Func<bool>? _canStart;

	private Point? _pressPoint;
	private bool _isDragging;

	/// <param name="handle">Where the press starts the gesture.</param>
	/// <param name="pressRouting">
	/// Bubble lets controls inside the handle (header buttons) keep their presses; Tunnel sees the
	/// press before the handle itself handles it (a button as the handle).
	/// </param>
	/// <param name="canStart">False keeps the handle from starting a drag (a fullscreen widget).</param>
	public DragGesture(
		Control handle,
		RoutingStrategies pressRouting = RoutingStrategies.Bubble,
		Func<bool>? canStart = null
	)
	{
		_handle = handle;
		_canStart = canStart;
		handle.AddHandler(InputElement.PointerPressedEvent, OnPressed, pressRouting);
		handle.AddHandler(
			InputElement.PointerMovedEvent,
			OnMoved,
			RoutingStrategies.Tunnel,
			handledEventsToo: true
		);
		handle.AddHandler(
			InputElement.PointerReleasedEvent,
			OnReleased,
			RoutingStrategies.Tunnel,
			handledEventsToo: true
		);
		handle.PointerCaptureLost += OnCaptureLost;
	}

	/// <summary>Where on the handle it was grabbed, so a floating window lands under the pointer.</summary>
	public Point GrabOffset { get; private set; }

	// Screen positions (device pixels): the drag can cross windows, e.g. from a floating one
	public event Action<PixelPoint>? Started;
	public event Action<PixelPoint>? Moved;
	public event Action<PixelPoint>? Dropped;
	public event Action? Cancelled;

	private void OnPressed(object? sender, PointerPressedEventArgs e)
	{
		if (
			!e.GetCurrentPoint(_handle).Properties.IsLeftButtonPressed
			|| _canStart?.Invoke() == false
		)
			return;
		_pressPoint = e.GetPosition(_handle);
		e.Pointer.Capture(_handle);
	}

	private void OnMoved(object? sender, PointerEventArgs e)
	{
		if (_pressPoint is not { } press)
			return;
		var position = e.GetPosition(_handle);
		if (!_isDragging)
		{
			var delta = position - press;
			if (!WorkspaceHelpers.IsBeyondDragThreshold(delta.X, delta.Y))
				return;
			_isDragging = true;
			GrabOffset = press;
			_handle.Cursor = new Cursor(StandardCursorType.DragMove);
			Started?.Invoke(_handle.PointToScreen(position));
		}
		Moved?.Invoke(_handle.PointToScreen(position));
	}

	private void OnReleased(object? sender, PointerReleasedEventArgs e)
	{
		if (_pressPoint is null)
			return;
		var wasDragging = _isDragging;
		var screen = _handle.PointToScreen(e.GetPosition(_handle));
		Reset();
		// A plain click keeps the capture until the release is done (Avalonia ends it then): a
		// button handle that lost it now would not click
		if (!wasDragging)
			return;
		// The drop is the whole gesture: a button handle must not click on it too
		e.Handled = true;
		e.Pointer.Capture(null);
		Dropped?.Invoke(screen);
	}

	private void OnCaptureLost(object? sender, PointerCaptureLostEventArgs e)
	{
		var wasDragging = _isDragging;
		Reset();
		if (wasDragging)
			Cancelled?.Invoke();
	}

	private void Reset()
	{
		_pressPoint = null;
		_isDragging = false;
		_handle.Cursor = null;
	}
}
