using Avalonia.Controls;
using Avalonia.Interactivity;

namespace LT.App.Controls;

/// <summary>What the user picked in a <see cref="ConfirmDialog"/>.</summary>
public enum ConfirmChoice
{
	Cancel,
	Confirm,
	Alternative,
}

/// <summary>
/// A small modal "are you sure?" over a window: a message, a line of detail, Cancel and a confirm
/// button (Enter confirms, Esc or closing the dialog cancels). <see cref="ShowAsync"/> asks before
/// something that can't be undone (red confirm, e.g. closing a view with its widgets);
/// <see cref="ShowChoiceAsync"/> adds a second, red way to go on (e.g. Close this window /
/// Close application).
/// </summary>
public partial class ConfirmDialog : Window
{
	public ConfirmDialog() => InitializeComponent();

	/// <summary>True when the user confirmed.</summary>
	public static async Task<bool> ShowAsync(
		Window owner,
		string title,
		string message,
		string detail,
		string confirmText
	)
	{
		var dialog = Create(title, message, detail, confirmText);
		dialog.ConfirmButton.Classes.Add("danger");
		return await dialog.ShowDialog<ConfirmChoice>(owner) == ConfirmChoice.Confirm;
	}

	/// <summary>
	/// Cancel, <paramref name="alternativeText"/> (red) and <paramref name="confirmText"/> (the
	/// default, on Enter).
	/// </summary>
	public static Task<ConfirmChoice> ShowChoiceAsync(
		Window owner,
		string title,
		string message,
		string detail,
		string confirmText,
		string alternativeText
	)
	{
		var dialog = Create(title, message, detail, confirmText);
		dialog.ConfirmButton.Classes.Add("accent");
		dialog.AlternativeButton.Content = alternativeText;
		dialog.AlternativeButton.IsVisible = true;
		return dialog.ShowDialog<ConfirmChoice>(owner);
	}

	private static ConfirmDialog Create(
		string title,
		string message,
		string detail,
		string confirmText
	)
	{
		var dialog = new ConfirmDialog { Title = title };
		dialog.MessageText.Text = message;
		dialog.DetailText.Text = detail;
		dialog.DetailText.IsVisible = detail.Length > 0;
		dialog.ConfirmButton.Content = confirmText;
		return dialog;
	}

	private void OnConfirmClick(object? sender, RoutedEventArgs e) => Close(ConfirmChoice.Confirm);

	private void OnAlternativeClick(object? sender, RoutedEventArgs e) =>
		Close(ConfirmChoice.Alternative);

	// Closing the dialog any other way gives default(ConfirmChoice): Cancel
	private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(ConfirmChoice.Cancel);
}
