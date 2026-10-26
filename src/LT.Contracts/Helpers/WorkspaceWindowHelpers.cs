using LT.Contracts.Constants;
using LT.Contracts.Dtos.Settings;

namespace LT.Contracts.Helpers;

/// <summary>
/// Extra workspace windows: each has a workspace of its own (side panels, widgets, center views)
/// next to the main window's. Widget instances belong to one window; any widget can be open in
/// several windows at once.
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

	/// <summary>
	/// The line under "Close this window or the whole application?" when ✕ is clicked with
	/// <paramref name="windowCount"/> workspace windows open (main included). Closing the main window
	/// on its own hands it over to <paramref name="nextWindowName"/>.
	/// </summary>
	public static string GetCloseDetail(bool isMain, string? nextWindowName, int windowCount)
	{
		var thisWindow = isMain
			? $"Close this window closes its views and widgets; {nextWindowName} takes its place as the main window."
			: "Close this window closes its views and widgets.";
		return $"{thisWindow} Close application keeps all {windowCount} windows for the next start.";
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
	/// (<see cref="WorkspaceHelpers.Normalize"/>).
	/// </summary>
	public static void Normalize(LayoutDto layout)
	{
		layout.Widgets = WorkspaceHelpers.Normalize(layout.Widgets);
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

			window.Widgets = WorkspaceHelpers.Normalize(window.Widgets);
		}
	}
}
