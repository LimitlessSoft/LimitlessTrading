using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Threading;
using LT.Contracts.Dtos.Settings;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;

namespace LT.App.Shell;

/// <summary>
/// Puts a window back where it was saved (<see cref="Restore"/>) and keeps, while it is open, what
/// gets saved of it (<see cref="Capture"/>): its normal-state bounds — maximized or full-screen
/// bounds are never taken for them, so un-maximizing after a restart goes back to the right size —
/// its last state other than minimized, and the screen it is on.
/// </summary>
public sealed class WindowPlacementTracker
{
	private readonly Window _window;
	private double? _x;
	private double? _y;
	private double _width;
	private double _height;
	private WindowPlacementState _state;
	private bool _capturePending;

	// Set by Restore before the window shows: the saved state goes on once it is open
	private bool _applyStateOnOpen;

	/// <summary>Follows <paramref name="window"/> from now on; call before it is shown.</summary>
	public WindowPlacementTracker(Window window)
	{
		_window = window;
		_width = window.Width;
		_height = window.Height;
		window.PositionChanged += (_, _) => ScheduleCapture();
		window.PropertyChanged += OnWindowPropertyChanged;
		window.Opened += (_, _) =>
		{
			if (!_applyStateOnOpen)
				return;
			_applyStateOnOpen = false;
			ApplyState();
		};
	}

	/// <summary>
	/// Sets the window to <paramref name="saved"/>, fitted to the screens connected now
	/// (<see cref="WindowPlacementHelpers.Resolve"/>): the normal bounds first, on the saved screen,
	/// and the state (maximized, full screen) only once the window shows there, so it maximizes on
	/// that screen. Works on a window already open too. Without a saved position it opens at
	/// <paramref name="defaultPosition"/> (kept on a screen), or at the window's own startup
	/// location when that is null.
	/// </summary>
	public void Restore(WindowPlacementDto saved, PixelPoint? defaultPosition = null)
	{
		if (saved.X is null && defaultPosition is { } position)
			saved = new WindowPlacementDto
			{
				X = position.X,
				Y = position.Y,
				Width = saved.Width,
				Height = saved.Height,
				State = saved.State,
			};
		var placement = WindowPlacementHelpers.Resolve(saved, GetScreens(_window));
		_window.WindowState = WindowState.Normal;
		_window.Width = Math.Max(_window.MinWidth, placement.Width);
		_window.Height = Math.Max(_window.MinHeight, placement.Height);
		if (placement is { X: { } x, Y: { } y })
		{
			_window.WindowStartupLocation = WindowStartupLocation.Manual;
			_window.Position = new PixelPoint((int)x, (int)y);
		}

		_x = placement.X;
		_y = placement.Y;
		_width = _window.Width;
		_height = _window.Height;
		_state = placement.State;
		if (_window.IsVisible)
			ApplyState();
		else
			_applyStateOnOpen = true;
	}

	/// <summary>What to save of the window now: normal bounds, state and screen.</summary>
	public WindowPlacementDto Capture()
	{
		CaptureNormalBounds();
		return new WindowPlacementDto
		{
			X = _x,
			Y = _y,
			Width = _width,
			Height = _height,
			State = _state,
			Screen = GetCurrentScreen() is { } screen
				? WindowPlacementHelpers.ToSaved(ToDto(screen))
				: null,
		};
	}

	/// <summary>The screens connected now, as the placement helpers take them.</summary>
	public static IReadOnlyList<ScreenDto> GetScreens(Window window) =>
		window.Screens.All.Select(ToDto).ToList();

	// The bounds it opened at are the normal ones (also when centered by the platform), then the
	// saved state goes on
	private void ApplyState()
	{
		CaptureNormalBounds();
		_window.WindowState = _state switch
		{
			WindowPlacementState.Maximized => WindowState.Maximized,
			WindowPlacementState.FullScreen => WindowState.FullScreen,
			_ => WindowState.Normal,
		};
	}

	private void OnWindowPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
	{
		if (e.Property == Window.WindowStateProperty)
		{
			// Minimized comes back as what it was before
			_state = _window.WindowState switch
			{
				WindowState.Maximized => WindowPlacementState.Maximized,
				WindowState.FullScreen => WindowPlacementState.FullScreen,
				WindowState.Normal => WindowPlacementState.Normal,
				_ => _state,
			};
			ScheduleCapture();
		}
		else if (e.Property == TopLevel.ClientSizeProperty)
			ScheduleCapture();
	}

	// Taken once the platform is done: maximizing moves and resizes the window before the state
	// says maximized, and those bounds must not count as normal ones
	private void ScheduleCapture()
	{
		if (_capturePending)
			return;
		_capturePending = true;
		Dispatcher.UIThread.Post(
			() =>
			{
				_capturePending = false;
				CaptureNormalBounds();
			},
			DispatcherPriority.Background
		);
	}

	private void CaptureNormalBounds()
	{
		if (!_window.IsVisible || _window.WindowState != WindowState.Normal)
			return;
		_x = _window.Position.X;
		_y = _window.Position.Y;
		_width = _window.ClientSize.Width;
		_height = _window.ClientSize.Height;
	}

	private Screen? GetCurrentScreen()
	{
		if (_window.Screens.ScreenFromWindow(_window) is { } screen)
			return screen;
		return _x is { } x && _y is { } y
			? _window.Screens.ScreenFromPoint(new PixelPoint((int)x, (int)y))
			: null;
	}

	private static ScreenDto ToDto(Screen screen) =>
		new()
		{
			Name = screen.DisplayName,
			Bounds = ToDto(screen.Bounds),
			WorkingArea = ToDto(screen.WorkingArea),
			Scaling = screen.Scaling,
			IsPrimary = screen.IsPrimary,
		};

	private static ScreenRectDto ToDto(PixelRect rect) =>
		new()
		{
			X = rect.X,
			Y = rect.Y,
			Width = rect.Width,
			Height = rect.Height,
		};
}
