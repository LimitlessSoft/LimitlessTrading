using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using LT.Contracts.Constants;

namespace LT.App.Widgets.Watchlist;

/// <summary>
/// The watchlist's header title: the shown watchlist, opening a list of all of them. A row is
/// picked by clicking its name, renamed in place (pencil; Enter or clicking away saves, Esc
/// cancels) or deleted after an inline confirmation (✕); below them, rows add, import and export
/// a watchlist, and the shown watchlist's custom columns are added, renamed and deleted the same
/// way.
/// </summary>
public partial class WatchlistTitleView : UserControl
{
	private static readonly FilePickerFileType WatchlistFileType = new("Watchlist")
	{
		Patterns = ["*" + WatchlistConstants.FileExtension],
		MimeTypes = ["application/json"],
	};

	private static readonly FilePickerFileType SymbolListFileType = new("Symbol list")
	{
		Patterns = ["*.txt", "*.csv"],
		MimeTypes = ["text/plain", "text/csv"],
	};

	public WatchlistTitleView() => InitializeComponent();

	private WatchlistViewModel? ViewModel => DataContext as WatchlistViewModel;

	private void OnExportClick(object? sender, RoutedEventArgs e) => _ = ExportAsync();

	private void OnImportClick(object? sender, RoutedEventArgs e) => _ = ImportAsync();

	private async Task ExportAsync()
	{
		PickerButton.Flyout?.Hide();
		if (
			ViewModel is not { } viewModel
			|| TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage
		)
			return;
		var file = await storage.SaveFilePickerAsync(
			new FilePickerSaveOptions
			{
				Title = "Export watchlist",
				SuggestedFileName = viewModel.GetExportFileName(),
				DefaultExtension = WatchlistConstants.FileExtension.TrimStart('.'),
				FileTypeChoices = [WatchlistFileType],
				ShowOverwritePrompt = true,
			}
		);
		if (file == null)
			return;
		try
		{
			var json = viewModel.ExportWatchlist();
			await using (var stream = await file.OpenWriteAsync())
			{
				if (stream.CanSeek)
					stream.SetLength(0);
				await using var writer = new StreamWriter(stream);
				await writer.WriteAsync(json);
			}
			viewModel.OnExported(file.Name, null);
		}
		catch (Exception ex)
		{
			viewModel.OnExported(file.Name, ex);
		}
		finally
		{
			file.Dispose();
		}
	}

	private async Task ImportAsync()
	{
		PickerButton.Flyout?.Hide();
		if (
			ViewModel is not { } viewModel
			|| TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage
		)
			return;
		var files = await storage.OpenFilePickerAsync(
			new FilePickerOpenOptions
			{
				Title = "Import watchlist",
				AllowMultiple = false,
				FileTypeFilter = [WatchlistFileType, SymbolListFileType, FilePickerFileTypes.All],
			}
		);
		if (files is not [var file])
			return;
		var fileName = file.Name;
		string content;
		try
		{
			// Read at most one character past the limit: the manager refuses bigger files
			var buffer = new char[WatchlistConstants.MaxFileLength + 1];
			int read;
			await using (var stream = await file.OpenReadAsync())
			{
				using var reader = new StreamReader(stream);
				read = await reader.ReadBlockAsync(buffer);
			}
			content = new string(buffer, 0, read);
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			viewModel.ShowFileError(ex.Message);
			return;
		}
		finally
		{
			file.Dispose();
		}
		await viewModel.ImportAsync(content, fileName);
	}

	// Switch first, then close: closing detaches the row, and with it its data
	private void OnRowPicked(object? sender, RoutedEventArgs e)
	{
		if (sender is Control { DataContext: WatchlistItemViewModel item })
			item.Owner.SelectWatchlistCommand.Execute(item);
		PickerButton.Flyout?.Hide();
	}

	// A name box opens focused with the name selected, ready to type over
	private void OnRenameBoxPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
	{
		if (e.Property != IsVisibleProperty || sender is not TextBox { IsVisible: true } box)
			return;
		Dispatcher.UIThread.Post(
			() =>
			{
				box.Focus();
				box.SelectAll();
			},
			DispatcherPriority.Loaded
		);
	}

	// Watchlist and custom column names alike
	private void OnRenameBoxKeyDown(object? sender, KeyEventArgs e)
	{
		if (e.Key is not (Key.Enter or Key.Escape) || sender is not TextBox box)
			return;
		var commit = e.Key == Key.Enter;
		switch (box.DataContext)
		{
			case WatchlistItemViewModel item when commit:
				item.Owner.CommitRenameCommand.Execute(item);
				break;
			case WatchlistItemViewModel item:
				item.Owner.CancelRenameCommand.Execute(item);
				break;
			case WatchlistColumnItemViewModel column when commit:
				column.Owner.CommitRenameColumnCommand.Execute(column);
				break;
			case WatchlistColumnItemViewModel column:
				column.Owner.CancelRenameColumnCommand.Execute(column);
				break;
			default:
				return;
		}
		e.Handled = true;
	}

	private void OnRenameBoxLostFocus(object? sender, RoutedEventArgs e)
	{
		switch ((sender as TextBox)?.DataContext)
		{
			case WatchlistItemViewModel item:
				item.Owner.CommitRenameCommand.Execute(item);
				break;
			case WatchlistColumnItemViewModel column:
				column.Owner.CommitRenameColumnCommand.Execute(column);
				break;
		}
	}
}
