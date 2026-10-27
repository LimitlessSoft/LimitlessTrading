using CommunityToolkit.Mvvm.ComponentModel;

namespace LT.App.Widgets.Watchlist;

/// <summary>A row's value in one custom column: what is saved, and the text while it is being edited.</summary>
public partial class WatchlistCellViewModel(string columnId, string text) : ObservableObject
{
	public string ColumnId => columnId;

	/// <summary>The saved value (empty: none).</summary>
	[ObservableProperty]
	public partial string Text { get; set; } = text;

	/// <summary>The cell's edit box; saved to <see cref="Text"/> only when the edit is committed.</summary>
	[ObservableProperty]
	public partial string EditText { get; set; } = text;
}
