using CommunityToolkit.Mvvm.ComponentModel;
using LT.Contracts.Dtos.Watchlists;

namespace LT.App.Widgets.Watchlist;

/// <summary>One row of the watchlist picker: select it, rename it in place, or delete it.</summary>
public partial class WatchlistItemViewModel(
	WatchlistViewModel owner,
	WatchlistDto watchlist,
	bool isActive
) : ObservableObject
{
	/// <summary>The widget: the row's buttons run its commands (the picker lives in a popup).</summary>
	public WatchlistViewModel Owner => owner;
	public WatchlistDto Watchlist => watchlist;
	public string Id => watchlist.Id;
	public string Name => watchlist.Name;
	public int Count => watchlist.Count;
	public bool IsActive => isActive;

	public string DeleteToolTip =>
		owner.CanDelete ? "Delete" : "The last watchlist can't be deleted";

	public string DeletePrompt =>
		watchlist.Count == 1
			? "Delete with its 1 symbol?"
			: $"Delete with its {watchlist.Count} symbols?";

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(IsIdle))]
	public partial bool IsRenaming { get; set; }

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(IsIdle))]
	public partial bool IsConfirmingDelete { get; set; }

	/// <summary>Neither renaming nor confirming: shows name, count and the row's buttons.</summary>
	public bool IsIdle => !IsRenaming && !IsConfirmingDelete;

	[ObservableProperty]
	public partial string RenameText { get; set; } = watchlist.Name;
}
