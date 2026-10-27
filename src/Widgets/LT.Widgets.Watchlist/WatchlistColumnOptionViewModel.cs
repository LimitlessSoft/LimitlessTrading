using CommunityToolkit.Mvvm.ComponentModel;
using LT.Contracts.Enums;

namespace LT.App.Widgets.Watchlist;

/// <summary>
/// A column, built-in or custom, as a row of the header popup's column list: shown or hidden
/// (check box) and sorted by (arrow), hidden or not. A custom column also renames and deletes
/// through <see cref="Custom"/>.
/// </summary>
public partial class WatchlistColumnOptionViewModel(
	WatchlistViewModel owner,
	WatchlistSortColumn column,
	string name,
	WatchlistColumnItemViewModel? custom
) : ObservableObject
{
	/// <summary>The widget: the row's buttons run its commands (the list lives in a popup).</summary>
	public WatchlistViewModel Owner => owner;

	/// <summary>The column; <see cref="WatchlistSortColumn.Custom"/> for a custom one.</summary>
	public WatchlistSortColumn Column => column;
	public string Name => name;

	/// <summary>The custom column (rename, delete); null for a built-in one.</summary>
	public WatchlistColumnItemViewModel? Custom => custom;
	public bool IsCustom => custom != null;
	public string? CustomColumnId => custom?.Id;

	// Set by the widget itself (loading, the header menu): not a click to pass on
	private bool _updating;

	/// <summary>Shown on the grid; flipping it shows or hides the column.</summary>
	[ObservableProperty]
	public partial bool IsShown { get; set; } = true;

	/// <summary>False for the last shown column: the grid keeps at least one.</summary>
	[ObservableProperty]
	public partial bool CanToggle { get; set; } = true;

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(SortGlyph), nameof(SortToolTip))]
	public partial bool IsSorted { get; set; }

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(SortGlyph), nameof(SortToolTip))]
	public partial bool IsDescending { get; set; }

	/// <summary>The sort button: the direction when sorted by this column, a faint ⇅ otherwise.</summary>
	public string SortGlyph =>
		!IsSorted ? "⇅"
		: IsDescending ? "↓"
		: "↑";

	public string SortToolTip =>
		!IsSorted ? "Sort by this column"
		: IsDescending ? "Sorted high to low; click for low to high"
		: "Sorted low to high; click for high to low";

	/// <summary>The widget's own update, not passed back to it.</summary>
	public void Update(bool isShown, bool canToggle, bool isSorted, bool isDescending)
	{
		_updating = true;
		try
		{
			IsShown = isShown;
			CanToggle = canToggle;
			IsSorted = isSorted;
			IsDescending = isDescending;
		}
		finally
		{
			_updating = false;
		}
	}

	partial void OnIsShownChanged(bool value)
	{
		if (!_updating)
			owner.SetColumnShown(this, value);
	}
}
