using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace LT.App.Controls;

/// <summary>
/// Ticker box with IB suggestions (chart title, watchlist). Enter picks the highlighted listing,
/// or enters the typed ticker when nothing that was searched for this text is highlighted; a click
/// on a listing picks it; Esc cancels.
/// </summary>
public partial class ContractSearchBox : UserControl
{
	// Text the dropdown's items were searched for; a highlighted item only counts for that text
	private string? _suggestedFor;

	// Enter / Esc already handled the box: the dropdown closing after them is not a pick
	private bool _ignoreClose;

	public ContractSearchBox()
	{
		InitializeComponent();
		Box.AsyncPopulator = PopulateAsync;
		Box.AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
		Box.DropDownClosed += OnDropDownClosed;
		Box.TextChanged += (_, _) => _ignoreClose = false;
	}

	/// <summary>Suggestions for the typed text (empty text: e.g. recents, or nothing).</summary>
	public Func<string?, CancellationToken, Task<IEnumerable<object>>>? Suggest { get; set; }

	public string? Placeholder
	{
		get => Box.PlaceholderText;
		set => Box.PlaceholderText = value;
	}

	public bool IsDropDownOpen
	{
		get => Box.IsDropDownOpen;
		set => Box.IsDropDownOpen = value;
	}

	/// <summary>True while the text box or its dropdown has keyboard focus.</summary>
	public bool HasFocus => Box.IsKeyboardFocusWithin;

	/// <summary>A listing was chosen (Enter on a highlighted one, or a click).</summary>
	public event Action<ContractSuggestionItem>? Picked;

	/// <summary>Enter on typed text with nothing searched for it highlighted.</summary>
	public event Action<string>? Entered;

	/// <summary>Esc.</summary>
	public event Action? Cancelled;

	/// <summary>Empties the box and offers <paramref name="items"/> (e.g. recents) until typing.</summary>
	public void Reset(IReadOnlyList<ContractSuggestionItem>? items = null)
	{
		Box.SelectedItem = null;
		Box.Text = string.Empty;
		Box.ItemsSource = items ?? [];
		_suggestedFor = string.Empty;
		// After the text change (which re-arms picks): closing now is not a pick
		_ignoreClose = true;
		Box.IsDropDownOpen = false;
	}

	public void FocusBox() => Box.Focus();

	private void OnKeyDown(object? sender, KeyEventArgs e)
	{
		switch (e.Key)
		{
			case Key.Escape:
				_ignoreClose = true;
				Cancelled?.Invoke();
				e.Handled = true;
				break;
			case Key.Enter:
				var text = Box.Text?.Trim() ?? string.Empty;
				_ignoreClose = true;
				if (
					Box.SelectedItem is ContractSuggestionItem item
					&& string.Equals(_suggestedFor, text, StringComparison.OrdinalIgnoreCase)
				)
					Picked?.Invoke(item);
				else if (text.Length > 0)
					Entered?.Invoke(text);
				e.Handled = true;
				break;
		}
	}

	// A click on a listing closes the dropdown on pointer release, but the list selects the
	// clicked listing only afterwards (on tap): read the selection once that input is done
	private void OnDropDownClosed(object? sender, EventArgs e)
	{
		if (_ignoreClose)
		{
			_ignoreClose = false;
			return;
		}
		Dispatcher.UIThread.Post(() =>
		{
			if (Box.SelectedItem is ContractSuggestionItem item)
				Picked?.Invoke(item);
		});
	}

	private async Task<IEnumerable<object>> PopulateAsync(
		string? text,
		CancellationToken cancellationToken
	)
	{
		if (Suggest is not { } suggest)
			return [];
		var items = await suggest(text, cancellationToken);
		_suggestedFor = text?.Trim() ?? string.Empty;
		return items;
	}
}
