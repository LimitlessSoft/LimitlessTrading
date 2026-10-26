using LT.Contracts.Dtos.Settings;

namespace LT.App.Workspace;

/// <summary>
/// What a workspace knows of the other windows: where one of its views can move to.
/// Implemented by <see cref="WorkspaceWindows"/>.
/// </summary>
public interface IWorkspaceHost
{
	/// <summary>Every window but the one of <paramref name="from"/>, main window first.</summary>
	IReadOnlyList<ViewMoveTarget> GetMoveTargets(WorkspaceViewModel from);

	/// <summary>
	/// Moves the view, with its widgets, into the workspace <paramref name="to"/>, at tab
	/// position <paramref name="slot"/> (null: after its tabs), and shows it there.
	/// </summary>
	void MoveView(CenterViewItem view, WorkspaceViewModel to, int? slot = null);

	/// <summary>
	/// Opens a new window holding only the view, with its widgets; at (<paramref name="x"/>,
	/// <paramref name="y"/>) in device pixels, or cascaded when they are null.
	/// </summary>
	void DetachView(CenterViewItem view, double? x = null, double? y = null);
}

/// <summary>A window a view can move to: its name and its workspace.</summary>
public sealed record ViewMoveTarget(string Name, WorkspaceViewModel Workspace);

/// <summary>
/// A view taken out of a workspace for another to adopt: its id, name, split tree and the center
/// widgets in it (view models and settings included).
/// </summary>
public sealed record CenterViewTransfer(
	string Id,
	string Name,
	CenterNodeDto? Root,
	IReadOnlyList<WidgetItem> Widgets
);
