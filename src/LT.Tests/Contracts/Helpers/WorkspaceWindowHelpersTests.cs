using LT.Contracts.Constants;
using LT.Contracts.Dtos.Settings;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;

namespace LT.Tests.Contracts.Helpers;

public class WorkspaceWindowHelpersTests
{
	private static WidgetPlacementDto Widget(WorkspaceTool tool, string? id = null) =>
		new()
		{
			Id = id ?? WorkspaceHelpers.GetDefaultId(tool),
			Tool = tool,
			Dock = WorkspaceHelpers.GetDefaultDock(tool),
		};

	[Fact]
	public void New_windows_are_numbered_from_2_skipping_names_in_use()
	{
		Assert.Equal("Window 2", WorkspaceWindowHelpers.GetNewName([]));
		Assert.Equal("Window 4", WorkspaceWindowHelpers.GetNewName(["window 2", "Window 3"]));
		Assert.Equal("Window 3", WorkspaceWindowHelpers.GetNewName(["Window 2", "Window 4"]));
	}

	[Fact]
	public void A_new_window_is_empty_with_the_default_size_and_its_own_id()
	{
		var first = WorkspaceWindowHelpers.CreateEmpty([]);
		var second = WorkspaceWindowHelpers.CreateEmpty([first.Name]);

		Assert.Empty(first.Widgets);
		Assert.Empty(first.Views);
		Assert.Equal(
			(null, null, null),
			(first.LeftWidgetId, first.RightWidgetId, first.BottomWidgetId)
		);
		Assert.Equal(
			(WorkspaceConstants.WindowDefaultWidth, WorkspaceConstants.WindowDefaultHeight, false),
			(first.Width, first.Height, first.IsMaximized)
		);
		Assert.Equal(("Window 2", "Window 3"), (first.Name, second.Name));
		Assert.NotEqual(first.Id, second.Id);
		Assert.False(string.IsNullOrEmpty(first.Id));
	}

	[Fact]
	public void Any_widget_can_be_open_in_several_windows()
	{
		var layout = new LayoutDto
		{
			Widgets = [Widget(WorkspaceTool.Chart), Widget(WorkspaceTool.Account)],
			Windows =
			[
				new WorkspaceWindowDto
				{
					Id = "a",
					Name = "Window 2",
					Width = 1000,
					Height = 700,
					Widgets = [Widget(WorkspaceTool.Account), Widget(WorkspaceTool.Watchlist)],
				},
				new WorkspaceWindowDto
				{
					Id = "b",
					Name = "Window 3",
					Width = 1000,
					Height = 700,
					Widgets = [Widget(WorkspaceTool.Watchlist), Widget(WorkspaceTool.News)],
				},
			],
		};

		WorkspaceWindowHelpers.Normalize(layout);

		Assert.Equal(
			[WorkspaceTool.Chart, WorkspaceTool.Account],
			layout.Widgets.Select(x => x.Tool)
		);
		Assert.Equal(
			[WorkspaceTool.Account, WorkspaceTool.Watchlist],
			layout.Windows[0].Widgets.Select(x => x.Tool)
		);
		Assert.Equal(
			[WorkspaceTool.Watchlist, WorkspaceTool.News],
			layout.Windows[1].Widgets.Select(x => x.Tool)
		);
	}

	[Fact]
	public void Charts_can_be_open_in_every_window()
	{
		var layout = new LayoutDto
		{
			Widgets = [Widget(WorkspaceTool.Chart)],
			Windows =
			[
				new WorkspaceWindowDto
				{
					Id = "a",
					Name = "Window 2",
					Width = 1000,
					Height = 700,
					Widgets =
					[
						Widget(WorkspaceTool.Chart),
						Widget(WorkspaceTool.Chart, "Chart-1234abcd"),
					],
				},
			],
		};

		WorkspaceWindowHelpers.Normalize(layout);

		Assert.Single(layout.Widgets);
		Assert.Equal(["Chart", "Chart-1234abcd"], layout.Windows[0].Widgets.Select(x => x.Id));
	}

	[Fact]
	public void Windows_get_unique_ids_names_and_a_usable_size()
	{
		var layout = new LayoutDto
		{
			Windows =
			[
				new WorkspaceWindowDto
				{
					Id = "same",
					Name = "  Charts  ",
					Width = 900,
					Height = 600,
				},
				new WorkspaceWindowDto { Id = "same", Name = " " },
				new WorkspaceWindowDto { Id = "", Name = "Window 2" },
			],
		};

		WorkspaceWindowHelpers.Normalize(layout);

		var windows = layout.Windows;
		Assert.Equal(3, windows.Select(x => x.Id).Distinct().Count());
		Assert.All(windows, x => Assert.False(string.IsNullOrEmpty(x.Id)));
		Assert.Equal("same", windows[0].Id);
		Assert.Equal(["Charts", "Window 3", "Window 2"], windows.Select(x => x.Name));
		Assert.Equal((900d, 600d), (windows[0].Width, windows[0].Height));
		Assert.Equal(
			(WorkspaceConstants.WindowDefaultWidth, WorkspaceConstants.WindowDefaultHeight),
			(windows[1].Width, windows[1].Height)
		);
	}

	[Fact]
	public void Closing_the_main_window_says_which_window_takes_its_place()
	{
		var detail = WorkspaceWindowHelpers.GetCloseDetail(true, "Window 2", 3);

		Assert.Contains("Window 2 takes its place as the main window", detail);
		Assert.Contains("keeps all 3 windows", detail);
	}

	[Fact]
	public void Closing_an_extra_window_says_only_its_views_and_widgets_go()
	{
		var detail = WorkspaceWindowHelpers.GetCloseDetail(false, null, 2);

		Assert.StartsWith("Close this window closes its views and widgets.", detail);
		Assert.DoesNotContain("main window", detail);
		Assert.Contains("keeps all 2 windows", detail);
	}
}
