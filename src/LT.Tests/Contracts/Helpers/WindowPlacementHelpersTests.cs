using LT.Contracts.Dtos.Settings;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;

namespace LT.Tests.Contracts.Helpers;

public class WindowPlacementHelpersTests
{
	// Two 1920×1080 screens side by side, the taskbar taking 40 px at the bottom of each
	private static ScreenDto Screen(
		string? name,
		double x,
		double y = 0,
		double width = 1920,
		double height = 1080,
		bool primary = false,
		double scaling = 1
	) =>
		new()
		{
			Name = name,
			Bounds = Rect(x, y, width, height),
			WorkingArea = Rect(x, y, width, height - 40),
			Scaling = scaling,
			IsPrimary = primary,
		};

	private static ScreenRectDto Rect(double x, double y, double width, double height) =>
		new()
		{
			X = x,
			Y = y,
			Width = width,
			Height = height,
		};

	private static WindowScreenDto Saved(string? name, double x, double y = 0) =>
		new()
		{
			Name = name,
			X = x,
			Y = y,
			Width = 1920,
			Height = 1080,
		};

	private static WindowPlacementDto Placement(
		double? x,
		double? y,
		double width = 800,
		double height = 600,
		WindowScreenDto? screen = null,
		WindowPlacementState state = WindowPlacementState.Normal
	) =>
		new()
		{
			X = x,
			Y = y,
			Width = width,
			Height = height,
			Screen = screen,
			State = state,
		};

	private static (double?, double?, double, double) Bounds(WindowPlacementDto placement) =>
		(placement.X, placement.Y, placement.Width, placement.Height);

	[Fact]
	public void The_saved_screen_is_found_by_name_and_bounds_even_listed_in_another_order()
	{
		var left = Screen("DP-1", 0, primary: true);
		var right = Screen("HDMI-1", 1920);

		Assert.Same(right, WindowPlacementHelpers.FindScreen(Saved("HDMI-1", 1920), [right, left]));
		Assert.Same(left, WindowPlacementHelpers.FindScreen(Saved("DP-1", 0), [right, left]));
	}

	[Fact]
	public void A_screen_whose_position_changed_is_found_by_its_name_and_a_renamed_one_by_its_bounds()
	{
		// HDMI-1 moved to the left of DP-1
		var moved = Screen("HDMI-1", -1920);
		var renamed = Screen("DISPLAY2", 0, primary: true);
		ScreenDto[] screens = [moved, renamed];

		Assert.Same(moved, WindowPlacementHelpers.FindScreen(Saved("HDMI-1", 1920), screens));
		Assert.Same(renamed, WindowPlacementHelpers.FindScreen(Saved("DP-1", 0), screens));
	}

	[Fact]
	public void Screens_sharing_a_name_are_told_apart_by_their_bounds()
	{
		var left = Screen("Generic PnP Monitor", 0, primary: true);
		var right = Screen("Generic PnP Monitor", 1920);

		Assert.Same(
			right,
			WindowPlacementHelpers.FindScreen(Saved("Generic PnP Monitor", 1920), [left, right])
		);
		// Same name twice and no bounds match: no way to tell which one it was
		Assert.Null(
			WindowPlacementHelpers.FindScreen(Saved("Generic PnP Monitor", 3840), [left, right])
		);
	}

	[Fact]
	public void A_screen_that_is_gone_or_was_never_saved_is_not_found()
	{
		ScreenDto[] screens = [Screen("DP-1", 0, primary: true)];

		Assert.Null(WindowPlacementHelpers.FindScreen(Saved("HDMI-1", 1920), screens));
		Assert.Null(WindowPlacementHelpers.FindScreen(null, screens));
	}

	[Fact]
	public void The_primary_screen_is_the_one_marked_so_else_the_first()
	{
		var first = Screen("A", 0);
		var primary = Screen("B", 1920, primary: true);

		Assert.Same(primary, WindowPlacementHelpers.GetPrimary([first, primary]));
		Assert.Same(first, WindowPlacementHelpers.GetPrimary([first, Screen("C", 1920)]));
		Assert.Null(WindowPlacementHelpers.GetPrimary([]));
	}

	[Fact]
	public void Clamping_moves_a_rect_inside_the_area_and_shrinks_it_when_too_big()
	{
		var area = Rect(0, 0, 1920, 1040);

		var moved = WindowPlacementHelpers.ClampToArea(Rect(1500, -50, 800, 600), area);
		var shrunk = WindowPlacementHelpers.ClampToArea(Rect(-100, 100, 2500, 1200), area);
		var inside = WindowPlacementHelpers.ClampToArea(Rect(100, 100, 800, 600), area);

		Assert.Equal((1120d, 0d, 800d, 600d), (moved.X, moved.Y, moved.Width, moved.Height));
		Assert.Equal((0d, 0d, 1920d, 1040d), (shrunk.X, shrunk.Y, shrunk.Width, shrunk.Height));
		Assert.Equal((100d, 100d, 800d, 600d), (inside.X, inside.Y, inside.Width, inside.Height));
	}

	[Fact]
	public void A_window_on_a_screen_still_there_comes_back_where_it_was()
	{
		ScreenDto[] screens = [Screen("DP-1", 0, primary: true), Screen("HDMI-1", 1920)];
		var saved = Placement(2100, 120, screen: Saved("HDMI-1", 1920));

		var placement = WindowPlacementHelpers.Resolve(saved, screens);

		Assert.Equal((2100d, 120d, 800d, 600d), Bounds(placement));
		Assert.Equal(("HDMI-1", 1920d), (placement.Screen?.Name, placement.Screen?.X));
	}

	[Fact]
	public void A_window_follows_its_screen_when_the_screens_are_rearranged()
	{
		// HDMI-1 was right of DP-1 and is now left of it: same spot on HDMI-1
		ScreenDto[] screens = [Screen("DP-1", 0, primary: true), Screen("HDMI-1", -1920)];
		var saved = Placement(2100, 120, screen: Saved("HDMI-1", 1920));

		var placement = WindowPlacementHelpers.Resolve(saved, screens);

		Assert.Equal((-1920d + 180, 120d, 800d, 600d), Bounds(placement));
		Assert.Equal(-1920d, placement.Screen?.X);
	}

	[Fact]
	public void A_window_whose_screen_is_gone_moves_onto_the_primary_screen()
	{
		ScreenDto[] screens = [Screen("DP-1", 0, primary: true)];
		var saved = Placement(
			1920 + 1500,
			700,
			screen: Saved("HDMI-1", 1920),
			state: WindowPlacementState.Maximized
		);

		var placement = WindowPlacementHelpers.Resolve(saved, screens);

		// Same spot relative to the screen's corner, pushed in so it fits the working area
		Assert.Equal((1120d, 440d, 800d, 600d), Bounds(placement));
		Assert.Equal(WindowPlacementState.Maximized, placement.State);
		Assert.Equal("DP-1", placement.Screen?.Name);
	}

	[Fact]
	public void A_window_bigger_than_its_screen_is_shrunk_to_the_working_area()
	{
		ScreenDto[] screens = [Screen("DP-1", 0, width: 1280, height: 800, primary: true)];
		var saved = Placement(0, 0, 1440, 900, Saved("DP-1", 0));
		saved.Screen!.Width = 1280;
		saved.Screen.Height = 800;

		var placement = WindowPlacementHelpers.Resolve(saved, screens);

		Assert.Equal((0d, 0d, 1280d, 760d), Bounds(placement));
	}

	[Fact]
	public void Sizes_are_dips_converted_with_the_target_screens_scaling()
	{
		// A 4K screen at 200 %: 1000×700 DIPs is 2000×1400 px and fits at its saved spot
		ScreenDto[] screens =
		[
			Screen("DP-1", 0, primary: true),
			Screen("DP-2", 1920, width: 3840, height: 2160, scaling: 2),
		];
		var savedScreen = Saved("DP-2", 1920);
		savedScreen.Width = 3840;
		savedScreen.Height = 2160;

		var fits = WindowPlacementHelpers.Resolve(
			Placement(3000, 200, 1000, 700, savedScreen),
			screens
		);
		var tooFarRight = WindowPlacementHelpers.Resolve(
			Placement(4500, 200, 1000, 700, savedScreen),
			screens
		);

		Assert.Equal((3000d, 200d, 1000d, 700d), Bounds(fits));
		Assert.Equal((5760d - 2000, 200d, 1000d, 700d), Bounds(tooFarRight));
	}

	[Fact]
	public void Without_a_saved_screen_a_window_goes_on_the_screen_it_overlaps_most()
	{
		ScreenDto[] screens = [Screen("DP-1", 0, primary: true), Screen("HDMI-1", 1920)];

		// Mostly on HDMI-1, hanging over its bottom edge
		var placement = WindowPlacementHelpers.Resolve(Placement(1800, 900), screens);

		Assert.Equal((1920d, 440d, 800d, 600d), Bounds(placement));
		Assert.Equal("HDMI-1", placement.Screen?.Name);
	}

	[Fact]
	public void Without_a_saved_screen_a_window_off_every_screen_is_centered_on_the_primary_one()
	{
		ScreenDto[] screens = [Screen("A", 0), Screen("B", 1920, primary: true)];

		var placement = WindowPlacementHelpers.Resolve(Placement(-5000, 3000), screens);

		Assert.Equal((1920d + 560, 220d, 800d, 600d), Bounds(placement));
		Assert.Equal("B", placement.Screen?.Name);
	}

	[Fact]
	public void Without_a_saved_position_the_default_placement_stays_and_only_the_size_is_fitted()
	{
		ScreenDto[] screens = [Screen("DP-1", 0, width: 1280, height: 800, primary: true)];

		var placement = WindowPlacementHelpers.Resolve(
			Placement(null, null, 1440, 900, state: WindowPlacementState.Maximized),
			screens
		);

		Assert.Equal((null, null, 1280d, 760d), Bounds(placement));
		Assert.Equal(WindowPlacementState.Maximized, placement.State);
	}

	[Fact]
	public void With_no_screens_known_the_saved_placement_is_used_as_is()
	{
		var saved = Placement(
			-5000,
			3000,
			2000,
			1500,
			Saved("X", 0),
			WindowPlacementState.FullScreen
		);

		var placement = WindowPlacementHelpers.Resolve(saved, []);

		Assert.Equal((-5000d, 3000d, 2000d, 1500d), Bounds(placement));
		Assert.Equal(WindowPlacementState.FullScreen, placement.State);
	}

	[Fact]
	public void Older_layouts_without_a_state_are_maximized_when_they_say_so()
	{
		var main = new LayoutDto
		{
			WindowWidth = 1440,
			WindowHeight = 900,
			IsMaximized = true,
		};
		var window = new WorkspaceWindowDto
		{
			X = 100,
			Y = 50,
			Width = 1280,
			Height = 800,
		};

		var mainPlacement = WindowPlacementHelpers.GetMain(main);
		var windowPlacement = WindowPlacementHelpers.GetWindow(window);

		Assert.Equal((null, null, 1440d, 900d), Bounds(mainPlacement));
		Assert.Equal(WindowPlacementState.Maximized, mainPlacement.State);
		Assert.Null(mainPlacement.Screen);
		Assert.Equal((100d, 50d, 1280d, 800d), Bounds(windowPlacement));
		Assert.Equal(WindowPlacementState.Normal, windowPlacement.State);
	}

	[Fact]
	public void A_saved_state_wins_over_is_maximized_and_saving_keeps_both_in_step()
	{
		var layout = new LayoutDto
		{
			IsMaximized = true,
			WindowState = WindowPlacementState.Normal,
		};
		Assert.Equal(WindowPlacementState.Normal, WindowPlacementHelpers.GetMain(layout).State);

		var screen = Saved("HDMI-1", 1920);
		WindowPlacementHelpers.SetMain(
			layout,
			Placement(2000, 100, 1200, 700, screen, WindowPlacementState.Maximized)
		);
		var window = new WorkspaceWindowDto();
		WindowPlacementHelpers.SetWindow(
			window,
			Placement(10, 20, 900, 650, screen, WindowPlacementState.FullScreen)
		);

		Assert.Equal(
			(2000d, 100d, 1200d, 700d, WindowPlacementState.Maximized, true),
			(
				layout.WindowX,
				layout.WindowY,
				layout.WindowWidth,
				layout.WindowHeight,
				layout.WindowState,
				layout.IsMaximized
			)
		);
		Assert.Same(screen, layout.WindowScreen);
		Assert.Equal(
			(10d, 20d, 900d, 650d, WindowPlacementState.FullScreen, false),
			(window.X, window.Y, window.Width, window.Height, window.State, window.IsMaximized)
		);
		Assert.Same(screen, window.Screen);
	}
}
