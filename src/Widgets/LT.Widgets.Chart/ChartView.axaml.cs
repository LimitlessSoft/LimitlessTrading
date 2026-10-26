using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Indicators;
using LT.Contracts.Helpers;

namespace LT.App.Widgets.Chart;

/// <summary>
/// The chart. Clicks in an indicator's legend row come here: the eye shows / hides it, ✕
/// removes it, the gear or the name opens its settings dialog. Right-clicking the chart opens its
/// menu: "Manage indicators…" opens the indicator manager at the pointer — the chart's indicators,
/// each with ✕ to remove it (the manager stays open), then the picker for more (the built-ins and
/// the scripts, searchable — Enter adds the first match — and a link to the scripts folder; a full
/// chart shows the limit instead); "Apply
/// template" lists the templates (the default one starred); "Templates…" opens the template
/// manager at the pointer: apply one (click its name), make it the default for new charts (star),
/// rename (pencil; Enter or clicking away saves, Esc cancels), export it to a file, delete it (✕,
/// confirmed in place), save this chart as a new one, import a file or export them all. The files
/// are picked here; the view model reads and writes their content.
/// </summary>
public partial class ChartView : UserControl
{
	public ChartView()
	{
		InitializeComponent();
		Chart.IndicatorAction += OnIndicatorAction;
		ChartMenu.Opening += (_, _) => FillApplyTemplateMenu();
		ManageIndicatorsMenuItem.Click += (_, _) => ShowAtPointer(IndicatorsFlyout);
		ManageTemplatesMenuItem.Click += (_, _) => ShowAtPointer(TemplatesFlyout);
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

	// Set while the manager is shown again after a file dialog, to keep its result message
	private bool _reopeningTemplates;

	private Flyout IndicatorsFlyout => (Flyout)FlyoutBase.GetAttachedFlyout(Chart)!;

	private Flyout TemplatesFlyout => (Flyout)FlyoutBase.GetAttachedFlyout(DropRoot)!;

	// After the context menu has closed, so its closing doesn't take the new popup's focus
	private void ShowAtPointer(Flyout flyout) =>
		Dispatcher.UIThread.Post(() => flyout.ShowAt(Chart, showAtPointer: true));

	// Right-click: "Apply template" lists the templates as they are now (another chart may have
	// changed them); it is disabled while there are none
	private void FillApplyTemplateMenu()
	{
		ApplyTemplateMenuItem.Items.Clear();
		if (ViewModel is not { } viewModel)
			return;
		viewModel.RefreshTemplates();
		var star = this.FindResource("TemplateDefaultIcon") as Geometry;
		foreach (var template in viewModel.Templates)
		{
			var id = template.Id;
			var item = new MenuItem
			{
				Header = template.Name,
				Icon = template.IsDefault
					? new PathIcon
					{
						Width = 12,
						Height = 12,
						Data = star,
					}
					: null,
			};
			ToolTip.SetTip(item, template.Summary);
			item.Click += (_, _) => viewModel.ApplyTemplate(id);
			ApplyTemplateMenuItem.Items.Add(item);
		}
		ApplyTemplateMenuItem.IsEnabled = viewModel.HasTemplates;
	}

	private ChartViewModel? ViewModel => DataContext as ChartViewModel;

	private void OnIndicatorAction(string instanceId, ChartIndicatorAction action)
	{
		if (ViewModel is not { } viewModel)
			return;
		switch (action)
		{
			case ChartIndicatorAction.ToggleVisibility:
				viewModel.ToggleIndicator(instanceId);
				break;
			case ChartIndicatorAction.Remove:
				viewModel.RemoveIndicator(instanceId);
				break;
			case ChartIndicatorAction.OpenSettings:
				_ = OpenSettingsAsync(viewModel, instanceId);
				break;
		}
	}

	private async Task OpenSettingsAsync(ChartViewModel viewModel, string instanceId)
	{
		var (definition, instance) = viewModel.GetIndicator(instanceId);
		if (instance == null)
			return;
		// A script that is gone still opens, to say so
		definition ??= new IndicatorDefinitionDto
		{
			Id = instance.DefinitionId,
			Name = IndicatorHelpers.GetMissingLabel(instance.DefinitionId),
			Error = "This indicator is no longer available.",
		};
		var window = new IndicatorSettingsWindow(
			definition,
			instance,
			inputs => viewModel.UpdateIndicatorInputs(instanceId, inputs)
		);
		if (TopLevel.GetTopLevel(this) is Window owner)
			await window.ShowDialog(owner);
		else
			window.Show();
	}

	private void OnTemplatesOpening()
	{
		if (_reopeningTemplates)
		{
			_reopeningTemplates = false;
			return;
		}
		ViewModel?.RefreshTemplates();
	}

	// Centred on the chart: the pointer is wherever the file dialog left it
	private void ReopenTemplates()
	{
		_reopeningTemplates = true;
		TemplatesFlyout.ShowAt(Chart);
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

	private void OnIndicatorsOpening()
	{
		ViewModel?.RefreshChartIndicators();
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

	private void OnIndicatorRemoveClick(object? sender, RoutedEventArgs e)
	{
		if ((sender as Control)?.DataContext is ChartIndicatorItem item)
			ViewModel?.RemoveIndicator(item.InstanceId);
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
}
