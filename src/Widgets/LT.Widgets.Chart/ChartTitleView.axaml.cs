using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using LT.Contracts.Constants;

namespace LT.App.Widgets.Chart;

/// <summary>
/// The chart's header title: its ticker (<see cref="LT.App.Controls.TickerTitle"/>; clicking it
/// types another, leaving the box cancels) and the timeframe button beside it, which opens a menu
/// of the chart's timeframes; picking one reloads the bars at that size. ƒx opens the indicator
/// picker: the built-ins and the scripts, searchable (Enter adds the first match), and a link to
/// the scripts folder. The layers icon opens the chart templates: apply one (click its name), make
/// it the default for new charts (star), rename (pencil; Enter or clicking away saves, Esc
/// cancels), export it to a file, delete it (✕, confirmed in place), save this chart as a new one,
/// import a file or export them all. The files are picked here; the view model reads and writes
/// their content.
/// </summary>
public partial class ChartTitleView : UserControl
{
	public ChartTitleView()
	{
		InitializeComponent();
		DataContextChanged += (_, _) => FillTimeframeMenu();
		TimeframeMenu.Opening += (_, _) => CheckCurrentTimeframe();
		IndicatorsFlyout.Opening += (_, _) => OnIndicatorsOpening();
		IndicatorSearchBox.KeyDown += OnIndicatorSearchKeyDown;
		ScriptsFolderButton.Click += (_, _) => _ = OpenScriptsFolderAsync();
		TemplatesFlyout.Opening += (_, _) => OnTemplatesOpening();
		TemplateNameBox.KeyDown += OnTemplateNameKeyDown;
		ImportTemplatesButton.Click += (_, _) => _ = ImportTemplatesAsync();
		ExportAllTemplatesButton.Click += (_, _) => _ = ExportTemplatesAsync(null);
	}

	private static readonly FilePickerFileType TemplateFileType = new("Chart templates")
	{
		Patterns = ["*" + ChartTemplateConstants.FileExtension],
		MimeTypes = ["application/json"],
		AppleUniformTypeIdentifiers = ["public.json"],
	};

	// Set while the menu is shown again after a file dialog, to keep its result message
	private bool _reopeningTemplates;

	private Flyout TemplatesFlyout => (Flyout)TemplatesButton.Flyout!;

	private void OnTemplatesOpening()
	{
		if (_reopeningTemplates)
		{
			_reopeningTemplates = false;
			return;
		}
		ViewModel?.RefreshTemplates();
	}

	private void ReopenTemplates()
	{
		_reopeningTemplates = true;
		TemplatesFlyout.ShowAt(TemplatesButton);
	}

	// Apply first, then close: closing detaches the row, and with it its data
	private void OnTemplatePicked(object? sender, RoutedEventArgs e)
	{
		if (sender is Control { DataContext: ChartTemplateItemViewModel item })
			item.Owner.ApplyTemplate(item.Id);
		TemplatesFlyout.Hide();
	}

	private void OnTemplateNameKeyDown(object? sender, KeyEventArgs e)
	{
		if (e.Key != Key.Enter || ViewModel is not { } viewModel)
			return;
		e.Handled = true;
		viewModel.SaveTemplate();
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

	private void OnRenameBoxKeyDown(object? sender, KeyEventArgs e)
	{
		if (sender is not TextBox { DataContext: ChartTemplateItemViewModel item })
			return;
		switch (e.Key)
		{
			case Key.Enter:
				item.Owner.CommitRenameTemplateCommand.Execute(item);
				e.Handled = true;
				break;
			case Key.Escape:
				item.Owner.CancelRenameTemplateCommand.Execute(item);
				e.Handled = true;
				break;
		}
	}

	private void OnRenameBoxLostFocus(object? sender, RoutedEventArgs e)
	{
		if (sender is TextBox { DataContext: ChartTemplateItemViewModel item })
			item.Owner.CommitRenameTemplateCommand.Execute(item);
	}

	private void OnExportTemplateClick(object? sender, RoutedEventArgs e)
	{
		if (sender is Control { DataContext: ChartTemplateItemViewModel item })
			_ = ExportTemplatesAsync(item.Id);
	}

	// One template (id) or all of them (null)
	private async Task ExportTemplatesAsync(string? id)
	{
		TemplatesFlyout.Hide();
		if (
			ViewModel is not { } viewModel
			|| TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage
		)
			return;
		var file = await storage.SaveFilePickerAsync(
			new FilePickerSaveOptions
			{
				Title = id != null ? "Export chart template" : "Export chart templates",
				SuggestedFileName = viewModel.GetTemplateFileName(id),
				DefaultExtension = ChartTemplateConstants.FileExtension.TrimStart('.'),
				FileTypeChoices = [TemplateFileType],
				ShowOverwritePrompt = true,
			}
		);
		if (file == null)
			return;
		try
		{
			var json = viewModel.ExportTemplates(id);
			await using (var stream = await file.OpenWriteAsync())
			{
				if (stream.CanSeek)
					stream.SetLength(0);
				await using var writer = new StreamWriter(stream);
				await writer.WriteAsync(json);
			}
			viewModel.OnTemplatesExported(
				file.Name,
				id != null ? 1 : viewModel.Templates.Count,
				null
			);
		}
		catch (Exception ex)
		{
			viewModel.OnTemplatesExported(file.Name, 0, ex);
		}
		finally
		{
			file.Dispose();
		}
		ReopenTemplates();
	}

	private async Task ImportTemplatesAsync()
	{
		TemplatesFlyout.Hide();
		if (
			ViewModel is not { } viewModel
			|| TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage
		)
			return;
		var files = await storage.OpenFilePickerAsync(
			new FilePickerOpenOptions
			{
				Title = "Import chart templates",
				AllowMultiple = false,
				FileTypeFilter = [TemplateFileType, FilePickerFileTypes.All],
			}
		);
		if (files is not [var file])
			return;
		try
		{
			// Read at most one character past the limit: the manager refuses bigger files
			var buffer = new char[ChartTemplateConstants.MaxFileLength + 1];
			int read;
			await using (var stream = await file.OpenReadAsync())
			{
				using var reader = new StreamReader(stream);
				read = await reader.ReadBlockAsync(buffer);
			}
			viewModel.ImportTemplates(new string(buffer, 0, read));
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			viewModel.ShowTemplateError(ex.Message);
		}
		finally
		{
			file.Dispose();
		}
		ReopenTemplates();
	}

	private Flyout IndicatorsFlyout => (Flyout)IndicatorsButton.Flyout!;

	private void OnIndicatorsOpening()
	{
		ViewModel?.RefreshIndicatorChoices();
		Dispatcher.UIThread.Post(
			() =>
			{
				IndicatorSearchBox.Focus();
				IndicatorSearchBox.SelectAll();
			},
			DispatcherPriority.Loaded
		);
	}

	private void OnIndicatorPicked(object? sender, RoutedEventArgs e)
	{
		if ((sender as Control)?.DataContext is IndicatorChoiceItem item)
			AddIndicator(item);
	}

	private void OnIndicatorSearchKeyDown(object? sender, KeyEventArgs e)
	{
		if (e.Key != Key.Enter || ViewModel?.IndicatorChoices is not [var first, ..])
			return;
		e.Handled = true;
		AddIndicator(first);
	}

	private void AddIndicator(IndicatorChoiceItem item)
	{
		IndicatorsFlyout.Hide();
		ViewModel?.AddIndicator(item.Id);
	}

	private async Task OpenScriptsFolderAsync()
	{
		IndicatorsFlyout.Hide();
		if (
			ViewModel is not { } viewModel
			|| TopLevel.GetTopLevel(this)?.Launcher is not { } launcher
		)
			return;
		var folder = new DirectoryInfo(viewModel.IndicatorScriptsDirectory);
		folder.Create();
		await launcher.LaunchDirectoryInfoAsync(folder);
	}

	private ChartViewModel? ViewModel => DataContext as ChartViewModel;

	private MenuFlyout TimeframeMenu => (MenuFlyout)TimeframeButton.Flyout!;

	// Filled up front: an empty MenuFlyout does not open, so filling it in Opening is too late
	private void FillTimeframeMenu()
	{
		TimeframeMenu.Items.Clear();
		if (ViewModel is not { } viewModel)
			return;
		foreach (var option in viewModel.Timeframes)
		{
			var item = new MenuItem
			{
				Header = option.ToString(),
				Tag = option,
				ToggleType = MenuItemToggleType.Radio,
			};
			item.Click += (_, _) => viewModel.SetTimeframe(option.Value);
			TimeframeMenu.Items.Add(item);
		}
	}

	// The check mark follows the chart's current timeframe
	private void CheckCurrentTimeframe()
	{
		foreach (var item in TimeframeMenu.Items.OfType<MenuItem>())
			item.IsChecked = item.Tag == ViewModel?.SelectedTimeframe;
	}
}
