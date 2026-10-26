using LT.Contracts.Constants;
using LT.Contracts.Dtos.Settings;

namespace LT.Contracts.Helpers;

/// <summary>
/// Extra workspace windows: each has a workspace of its own (side panels, widgets, center views)
/// next to the main window's. A single-instance widget exists once in the whole app.
/// </summary>
public static class WorkspaceWindowHelpers
{
	public static string NewId() => Guid.NewGuid().ToString("N");

	/// <summary>The first free "Window N" (N from 2: the main window is the first).</summary>
	public static string GetNewName(IEnumerable<string> existing)
	{
		var names = existing.ToHashSet(StringComparer.OrdinalIgnoreCase);
		for (var n = 2; ; n++)
		{
			var name = $"{WorkspaceConstants.NewWindowNamePrefix} {n}";
			if (!names.Contains(name))
				return name;
		}
	}

	/// <summary>A new, empty window: no widgets, every side collapsed, the default size.</summary>
	public static WorkspaceWindowDto CreateEmpty(IEnumerable<string> existingNames) =>
		new()
		{
			Id = NewId(),
			Name = GetNewName(existingNames),
			LeftWidth = WorkspaceConstants.DefaultLeftWidth,
			RightWidth = WorkspaceConstants.DefaultRightWidth,
			BottomHeight = WorkspaceConstants.DefaultBottomHeight,
			Width = WorkspaceConstants.WindowDefaultWidth,
			Height = WorkspaceConstants.WindowDefaultHeight,
		};

	/// <summary>
	/// Cleans the saved windows of <paramref name="layout"/>: unique non-empty ids, non-blank names,
	/// a usable size, and each window's widgets cleaned like the main one's
	/// (<see cref="WorkspaceHelpers.Normalize"/>). A single-instance widget already in the main
	/// window or in an earlier window is dropped from the later one.
	/// </summary>
	public static void Normalize(LayoutDto layout)
	{
		layout.Widgets = WorkspaceHelpers.Normalize(layout.Widgets);
		var taken = layout
			.Widgets.Where(x => !WorkspaceHelpers.AllowsMultipleInstances(x.Tool))
			.Select(x => x.Tool)
			.ToHashSet();
		var ids = new HashSet<string>();
		var names = new List<string>();
		foreach (var window in layout.Windows)
		{
			if (string.IsNullOrEmpty(window.Id) || !ids.Add(window.Id))
				ids.Add(window.Id = NewId());
			window.Name = string.IsNullOrWhiteSpace(window.Name)
				? GetNewName(names.Concat(layout.Windows.Select(x => x.Name)))
				: window.Name.Trim();
			names.Add(window.Name);
			if (window.Width < WorkspaceConstants.WindowMinWidth)
				window.Width = WorkspaceConstants.WindowDefaultWidth;
			if (window.Height < WorkspaceConstants.WindowMinHeight)
				window.Height = WorkspaceConstants.WindowDefaultHeight;

			window.Widgets = WorkspaceHelpers
				.Normalize(window.Widgets)
				.Where(x => WorkspaceHelpers.AllowsMultipleInstances(x.Tool) || taken.Add(x.Tool))
				.ToList();
		}
	}
}
