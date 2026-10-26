using System.Text.Json;
using LT.Contracts.Entities;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;

namespace LT.Tests.Contracts.Entities;

public class LayoutEntityTests
{
	[Fact]
	public void A_layout_saved_before_workspace_windows_loads_with_none()
	{
		const string json = """
			{
			  "Layout": {
			    "LeftWidth": 300,
			    "Widgets": [ { "Id": "Chart", "Tool": 6, "Dock": 0 } ],
			    "IsMaximized": false
			  }
			}
			""";

		var layout = JsonSerializer.Deserialize<SettingsEntity>(json)!.Layout;

		Assert.Empty(layout.Windows);
		Assert.Equal((300d, false), (layout.LeftWidth, layout.IsMaximized));
		Assert.Equal(WorkspaceTool.Chart, Assert.Single(layout.Widgets).Tool);
	}

	[Fact]
	public void A_fresh_main_layout_has_the_first_run_widgets_but_a_window_starts_empty()
	{
		Assert.Equal(
			WorkspaceHelpers.GetDefaultPlacements().Select(x => x.Tool),
			new LayoutEntity().Widgets.Select(x => x.Tool)
		);
		Assert.Empty(new WorkspaceWindowEntity().Widgets);

		// A saved window without a widget list doesn't pick up the first-run widgets either
		var window = JsonSerializer.Deserialize<WorkspaceWindowEntity>("""{ "Name": "W" }""")!;
		Assert.Empty(window.Widgets);
	}

	[Fact]
	public void Workspace_windows_survive_a_save_and_load()
	{
		var layout = new LayoutEntity
		{
			Windows =
			[
				new WorkspaceWindowEntity
				{
					Id = "w2",
					Name = "Window 2",
					X = 1920,
					Y = 40,
					Width = 1200,
					Height = 760,
					IsMaximized = true,
					RightWidgetId = "News",
					Widgets =
					[
						new WidgetPlacementEntity
						{
							Id = "News",
							Tool = WorkspaceTool.News,
							Dock = WidgetDock.Right,
						},
					],
				},
			],
		};

		var loaded = JsonSerializer.Deserialize<LayoutEntity>(JsonSerializer.Serialize(layout))!;

		var window = Assert.Single(loaded.Windows);
		Assert.Equal(
			("w2", "Window 2", 1920d, 40d, 1200d, 760d, true, "News"),
			(
				window.Id,
				window.Name,
				window.X,
				window.Y,
				window.Width,
				window.Height,
				window.IsMaximized,
				window.RightWidgetId
			)
		);
		Assert.Equal(
			(WorkspaceTool.News, WidgetDock.Right),
			(Assert.Single(window.Widgets).Tool, window.Widgets[0].Dock)
		);
	}

	[Fact]
	public void A_layout_saved_before_window_placement_loads_without_screen_or_state()
	{
		const string json = """
			{
			  "Layout": {
			    "WindowWidth": 1300,
			    "WindowHeight": 850,
			    "IsMaximized": true,
			    "Widgets": [ { "Id": "Chart", "Tool": 6, "Dock": 3, "FloatX": 50, "FloatY": 60 } ],
			    "Windows": [ { "Id": "w2", "Name": "Window 2", "X": 1920, "Y": 40, "IsMaximized": true } ]
			  }
			}
			""";

		var layout = JsonSerializer.Deserialize<SettingsEntity>(json)!.Layout;

		Assert.Equal(
			(null, null, null, null, 1300d, 850d, true),
			(
				layout.WindowX,
				layout.WindowY,
				layout.WindowState,
				layout.WindowScreen,
				layout.WindowWidth,
				layout.WindowHeight,
				layout.IsMaximized
			)
		);
		var window = Assert.Single(layout.Windows);
		Assert.Equal((null, null, true), (window.State, window.Screen, window.IsMaximized));
		var widget = Assert.Single(layout.Widgets);
		Assert.Equal((null, null, 50d), (widget.FloatState, widget.FloatScreen, widget.FloatX));
	}

	[Fact]
	public void Window_placements_survive_a_save_and_load()
	{
		var layout = new LayoutEntity
		{
			WindowX = -1800,
			WindowY = 30,
			WindowWidth = 1500,
			WindowHeight = 950,
			WindowState = WindowPlacementState.FullScreen,
			WindowScreen = new WindowScreenEntity
			{
				Name = "HDMI-1",
				X = -1920,
				Width = 1920,
				Height = 1080,
			},
			Widgets =
			[
				new WidgetPlacementEntity
				{
					Id = "News",
					Tool = WorkspaceTool.News,
					Dock = WidgetDock.Floating,
					FloatState = WindowPlacementState.Maximized,
					FloatScreen = new WindowScreenEntity { Name = "DP-1", Width = 2560 },
				},
			],
			Windows =
			[
				new WorkspaceWindowEntity
				{
					Id = "w2",
					State = WindowPlacementState.Maximized,
					Screen = new WindowScreenEntity { Name = "DP-2", X = 2560 },
				},
			],
		};

		var loaded = JsonSerializer.Deserialize<LayoutEntity>(JsonSerializer.Serialize(layout))!;

		Assert.Equal(
			(-1800d, 30d, 1500d, 950d, WindowPlacementState.FullScreen),
			(
				loaded.WindowX,
				loaded.WindowY,
				loaded.WindowWidth,
				loaded.WindowHeight,
				loaded.WindowState
			)
		);
		Assert.Equal(
			("HDMI-1", -1920d, 0d, 1920d, 1080d),
			(
				loaded.WindowScreen?.Name,
				loaded.WindowScreen?.X,
				loaded.WindowScreen?.Y,
				loaded.WindowScreen?.Width,
				loaded.WindowScreen?.Height
			)
		);
		var widget = Assert.Single(loaded.Widgets);
		Assert.Equal(
			(WindowPlacementState.Maximized, "DP-1", 2560d),
			(widget.FloatState, widget.FloatScreen?.Name, widget.FloatScreen?.Width)
		);
		var window = Assert.Single(loaded.Windows);
		Assert.Equal(
			(WindowPlacementState.Maximized, "DP-2", 2560d),
			(window.State, window.Screen?.Name, window.Screen?.X)
		);
	}
}
