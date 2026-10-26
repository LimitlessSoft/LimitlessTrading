using Avalonia.Controls;
using Avalonia.Threading;
using LT.App.ViewModels;
using LT.App.Workspace;

namespace LT.App.Controls;

/// <summary>
/// A widget header title that is its ticker (chart, option chain). Clicking it swaps in a ticker
/// box: typing lists IB's listings of that symbol, an empty box (or the chevron) lists recent
/// tickers. Enter opens the highlighted listing, else the typed ticker (upper-cased, US listing
/// first; blank does nothing). Esc cancels; leaving the box cancels too, or commits the typed
/// ticker with <see cref="CommitOnLostFocus"/>. The state lives in the view model's
/// <see cref="TickerTitleEditor"/>; this only drives the box.
/// </summary>
public partial class TickerTitle : UserControl
{
	public TickerTitle()
	{
		InitializeComponent();
		TickerBox.Suggest = (text, ct) =>
			Editor?.GetSuggestionsAsync(text, ct) ?? Task.FromResult(Enumerable.Empty<object>());
		TickerBox.Picked += item => Finish(editor => editor.Pick(item.Contract));
		TickerBox.Entered += text => Finish(editor => _ = editor.CommitAsync(text));
		TickerBox.Cancelled += () => Finish(editor => editor.Cancel());
		TitleButton.Click += (_, _) => BeginEdit();
		RecentButton.Click += (_, _) => TickerBox.IsDropDownOpen = !TickerBox.IsDropDownOpen;
		TickerBox.LostFocus += (_, _) => Dispatcher.UIThread.Post(EndEditIfUnfocused);
	}

	/// <summary>Leaving the box opens the typed ticker instead of cancelling.</summary>
	public bool CommitOnLostFocus { get; init; }

	private TickerTitleEditor? Editor => (DataContext as ITickerTitled)?.TickerEditor;

	private void BeginEdit()
	{
		if (Editor is not { } editor)
			return;
		var recents = editor.GetRecentSuggestions();
		TickerBox.Reset(recents);
		editor.Begin();
		Dispatcher.UIThread.Post(
			() =>
			{
				TickerBox.FocusBox();
				TickerBox.IsDropDownOpen = recents.Count > 0;
			},
			DispatcherPriority.Loaded
		);
	}

	// The box is emptied first, so its dropdown closing is not taken as a pick
	private void Finish(Action<TickerTitleEditor> action)
	{
		TickerBox.Reset();
		if (Editor is { } editor)
			action(editor);
	}

	// Focus can move to the dropdown or the chevron without leaving the editor
	private void EndEditIfUnfocused()
	{
		if (
			Editor is not { IsEditing: true }
			|| TickerBox.IsDropDownOpen
			|| RecentButton.IsFocused
			|| TickerBox.HasFocus
		)
			return;
		var text = TickerBox.Text;
		if (CommitOnLostFocus)
			Finish(editor => _ = editor.CommitAsync(text));
		else
			Finish(editor => editor.Cancel());
	}
}
