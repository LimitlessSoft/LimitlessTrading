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
}
