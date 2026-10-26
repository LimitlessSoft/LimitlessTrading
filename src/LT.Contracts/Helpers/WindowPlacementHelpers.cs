using LT.Contracts.Dtos.Settings;
using LT.Contracts.Enums;

namespace LT.Contracts.Helpers;

/// <summary>
/// Saving and restoring where a window is (main, workspace and floating widget windows): its
/// normal-state bounds, state and screen. On restore the saved screen is looked up among the
/// screens connected now and the window is kept inside a visible working area.
/// </summary>
public static class WindowPlacementHelpers
{
	/// <summary>
	/// The screen among <paramref name="screens"/> that <paramref name="saved"/> describes: same
	/// name and bounds, else the only one with that name (its resolution or position changed), else
	/// the only one with those bounds (renamed). Null when it is gone or unknown.
	/// </summary>
	public static ScreenDto? FindScreen(WindowScreenDto? saved, IReadOnlyList<ScreenDto> screens)
	{
		if (saved is null)
			return null;
		var hasName = !string.IsNullOrEmpty(saved.Name);
		if (
			hasName
			&& screens.FirstOrDefault(x => HasName(x, saved) && HasBounds(x, saved)) is { } exact
		)
			return exact;
		if (hasName && screens.Where(x => HasName(x, saved)).ToList() is [var named])
			return named;
		return screens.Where(x => HasBounds(x, saved)).ToList() is [var placed] ? placed : null;
	}

	/// <summary>The primary screen; the first one when none says it is.</summary>
	public static ScreenDto? GetPrimary(IReadOnlyList<ScreenDto> screens) =>
		screens.FirstOrDefault(x => x.IsPrimary) ?? screens.FirstOrDefault();

	/// <summary>What gets saved of <paramref name="screen"/> to find it again.</summary>
	public static WindowScreenDto ToSaved(ScreenDto screen) =>
		new()
		{
			Name = screen.Name,
			X = screen.Bounds.X,
			Y = screen.Bounds.Y,
			Width = screen.Bounds.Width,
			Height = screen.Bounds.Height,
		};

	/// <summary>
	/// <paramref name="rect"/> moved (and shrunk when bigger) so that it lies inside
	/// <paramref name="area"/>.
	/// </summary>
	public static ScreenRectDto ClampToArea(ScreenRectDto rect, ScreenRectDto area)
	{
		var width = Math.Min(rect.Width, area.Width);
		var height = Math.Min(rect.Height, area.Height);
		return new ScreenRectDto
		{
			X = Math.Clamp(rect.X, area.X, area.Right - width),
			Y = Math.Clamp(rect.Y, area.Y, area.Bottom - height),
			Width = width,
			Height = height,
		};
	}

	/// <summary>Area (pixels²) <paramref name="a"/> and <paramref name="b"/> share; 0 when apart.</summary>
	public static double GetOverlap(ScreenRectDto a, ScreenRectDto b) =>
		Math.Max(0, Math.Min(a.Right, b.Right) - Math.Max(a.X, b.X))
		* Math.Max(0, Math.Min(a.Bottom, b.Bottom) - Math.Max(a.Y, b.Y));

	/// <summary>
	/// Where a window saved as <paramref name="saved"/> opens among <paramref name="screens"/>:
	/// <list type="bullet">
	/// <item>its saved screen still there: on it, at the same spot relative to the screen's corner
	/// (so it follows the screen when the screens are rearranged);</item>
	/// <item>its saved screen gone: on the primary screen, at that same relative spot;</item>
	/// <item>no saved screen (older layouts): on the screen it overlaps most, or centered on the
	/// primary screen when it overlaps none.</item>
	/// </list>
	/// Either way it is then clamped into that screen's working area (shrunk to fit). Width and
	/// height are DIPs, converted with the target screen's scaling. No saved position, or no screens
	/// known, leaves the position to the window's default placement (null) and only fits the size.
	/// The state and the screen it ends up on come with it.
	/// </summary>
	public static WindowPlacementDto Resolve(
		WindowPlacementDto saved,
		IReadOnlyList<ScreenDto> screens
	)
	{
		var result = new WindowPlacementDto
		{
			X = saved.X,
			Y = saved.Y,
			Width = saved.Width,
			Height = saved.Height,
			State = saved.State,
			Screen = saved.Screen,
		};
		if (GetPrimary(screens) is not { } primary)
			return result;
		if (saved is not { X: { } x, Y: { } y })
		{
			// Default placement: it opens on the primary screen (or near its owner), sized to fit
			var area = primary.WorkingArea;
			result.Width = Math.Min(saved.Width, area.Width / primary.Scaling);
			result.Height = Math.Min(saved.Height, area.Height / primary.Scaling);
			result.Screen = null;
			return result;
		}

		ScreenDto target;
		ScreenRectDto rect;
		if (saved.Screen is { } savedScreen)
		{
			target = FindScreen(savedScreen, screens) ?? primary;
			rect = new ScreenRectDto
			{
				X = target.Bounds.X + (x - savedScreen.X),
				Y = target.Bounds.Y + (y - savedScreen.Y),
				Width = saved.Width * target.Scaling,
				Height = saved.Height * target.Scaling,
			};
		}
		else
		{
			var best = screens
				.Select(s =>
					(
						Screen: s,
						Overlap: GetOverlap(
							new ScreenRectDto
							{
								X = x,
								Y = y,
								Width = saved.Width * s.Scaling,
								Height = saved.Height * s.Scaling,
							},
							s.WorkingArea
						)
					)
				)
				.MaxBy(s => s.Overlap);
			if (best.Overlap > 0)
			{
				target = best.Screen;
				rect = new ScreenRectDto
				{
					X = x,
					Y = y,
					Width = saved.Width * target.Scaling,
					Height = saved.Height * target.Scaling,
				};
			}
			else
			{
				// Off every screen: centered on the primary one
				target = primary;
				var width = saved.Width * target.Scaling;
				var height = saved.Height * target.Scaling;
				rect = new ScreenRectDto
				{
					X = target.WorkingArea.X + (target.WorkingArea.Width - width) / 2,
					Y = target.WorkingArea.Y + (target.WorkingArea.Height - height) / 2,
					Width = width,
					Height = height,
				};
			}
		}

		var fitted = ClampToArea(rect, target.WorkingArea);
		result.X = Math.Round(fitted.X);
		result.Y = Math.Round(fitted.Y);
		result.Width = fitted.Width / target.Scaling;
		result.Height = fitted.Height / target.Scaling;
		result.Screen = ToSaved(target);
		return result;
	}

	// Older layouts carry only IsMaximized: it decides when no state was saved
	private static WindowPlacementState GetState(WindowPlacementState? state, bool isMaximized) =>
		state ?? (isMaximized ? WindowPlacementState.Maximized : WindowPlacementState.Normal);

	/// <summary>The main window's saved placement (older layouts: centered, IsMaximized).</summary>
	public static WindowPlacementDto GetMain(LayoutDto layout) =>
		new()
		{
			X = layout.WindowX,
			Y = layout.WindowY,
			Width = layout.WindowWidth,
			Height = layout.WindowHeight,
			State = GetState(layout.WindowState, layout.IsMaximized),
			Screen = layout.WindowScreen,
		};

	/// <summary>Stores the main window's placement; IsMaximized keeps older versions reading it.</summary>
	public static void SetMain(LayoutDto layout, WindowPlacementDto placement)
	{
		layout.WindowX = placement.X;
		layout.WindowY = placement.Y;
		layout.WindowWidth = placement.Width;
		layout.WindowHeight = placement.Height;
		layout.WindowState = placement.State;
		layout.IsMaximized = placement.State == WindowPlacementState.Maximized;
		layout.WindowScreen = placement.Screen;
	}

	/// <summary>An extra workspace window's saved placement (older layouts: IsMaximized).</summary>
	public static WindowPlacementDto GetWindow(WorkspaceWindowDto window) =>
		new()
		{
			X = window.X,
			Y = window.Y,
			Width = window.Width,
			Height = window.Height,
			State = GetState(window.State, window.IsMaximized),
			Screen = window.Screen,
		};

	/// <summary>Stores an extra workspace window's placement.</summary>
	public static void SetWindow(WorkspaceWindowDto window, WindowPlacementDto placement)
	{
		window.X = placement.X;
		window.Y = placement.Y;
		window.Width = placement.Width;
		window.Height = placement.Height;
		window.State = placement.State;
		window.IsMaximized = placement.State == WindowPlacementState.Maximized;
		window.Screen = placement.Screen;
	}

	private static bool HasName(ScreenDto screen, WindowScreenDto saved) =>
		string.Equals(screen.Name, saved.Name, StringComparison.Ordinal);

	private static bool HasBounds(ScreenDto screen, WindowScreenDto saved) =>
		screen.Bounds.X == saved.X
		&& screen.Bounds.Y == saved.Y
		&& screen.Bounds.Width == saved.Width
		&& screen.Bounds.Height == saved.Height;
}
