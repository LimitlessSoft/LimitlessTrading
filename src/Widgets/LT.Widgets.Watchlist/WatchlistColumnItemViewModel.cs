using CommunityToolkit.Mvvm.ComponentModel;
using LT.Contracts.Dtos.Watchlists;

namespace LT.App.Widgets.Watchlist;

/// <summary>
/// A custom column, as a grid column and a row of the header popup's column list: rename it in
/// place or delete it.
/// </summary>
public partial class WatchlistColumnItemViewModel(
	WatchlistViewModel owner,
	WatchlistColumnDto column
) : ObservableObject
{
	/// <summary>The widget: the row's buttons run its commands (the list lives in a popup).</summary>
	public WatchlistViewModel Owner => owner;
	public string Id => column.Id;
	public string Name => column.Name;

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(IsIdle))]
	public partial bool IsRenaming { get; set; }

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(IsIdle))]
	public partial bool IsConfirmingDelete { get; set; }

	/// <summary>Neither renaming nor confirming: shows the name and the row's buttons.</summary>
	public bool IsIdle => !IsRenaming && !IsConfirmingDelete;

	[ObservableProperty]
	public partial string RenameText { get; set; } = column.Name;
}
