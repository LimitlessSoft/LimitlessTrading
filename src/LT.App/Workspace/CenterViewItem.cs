using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Settings;
using LT.Contracts.Helpers;

namespace LT.App.Workspace;

/// <summary>
/// One tab of the center ("view"): a name and its own split tree. Left, right, bottom and floating
/// widgets are shared by every view; a center widget sits in exactly one.
/// </summary>
public partial class CenterViewItem(WorkspaceViewModel workspace, string id, string name)
	: ObservableObject
{
	public WorkspaceViewModel Workspace => workspace;
	public string Id => id;

	[ObservableProperty]
	public partial string Name { get; set; } = name;

	/// <summary>The view's split tree; null when it is empty. Edited by the workspace.</summary>
	public CenterNodeDto? Root { get; set; }

	[ObservableProperty]
	public partial bool IsSelected { get; set; }

	/// <summary>The last view can't be closed.</summary>
	[ObservableProperty]
	public partial bool CanClose { get; set; }

	/// <summary>Not the first tab: Move left is offered.</summary>
	[ObservableProperty]
	public partial bool CanMoveLeft { get; set; }

	/// <summary>Not the last tab: Move right is offered.</summary>
	[ObservableProperty]
	public partial bool CanMoveRight { get; set; }

	/// <summary>Move to window and Detach are offered (the app's windows are known).</summary>
	public bool CanMoveOut => workspace.CanMoveViewsOut;

	[ObservableProperty]
	public partial bool IsRenaming { get; set; }

	[ObservableProperty]
	public partial string RenameText { get; set; } = name;

	public int NameMaxLength => WorkspaceConstants.ViewNameMaxLength;

	[RelayCommand]
	private void Select() => workspace.SelectView(this);

	[RelayCommand]
	private void Close() => workspace.CloseView(this);

	[RelayCommand]
	private void Duplicate() => workspace.DuplicateView(this);

	[RelayCommand]
	private void MoveLeft() => workspace.MoveViewBy(this, -1);

	[RelayCommand]
	private void MoveRight() => workspace.MoveViewBy(this, 1);

	[RelayCommand]
	private void MoveTo(WorkspaceViewModel to) => workspace.MoveViewTo(this, to);

	[RelayCommand]
	private void Detach() => workspace.DetachView(this);

	/// <summary>The windows this view can move to, for its right-click menu.</summary>
	public IReadOnlyList<ViewMoveTarget> GetMoveTargets() => workspace.GetMoveTargets();

	/// <summary>How many widgets go with it when it is closed.</summary>
	public int WidgetCount => CenterLayoutHelpers.GetWidgetIds(Root).Count();

	[RelayCommand]
	private void BeginRename() => workspace.BeginRenameView(this);

	[RelayCommand]
	private void CommitRename() => workspace.CommitRenameView(this);

	[RelayCommand]
	private void CancelRename() => workspace.CancelRenameView(this);
}
