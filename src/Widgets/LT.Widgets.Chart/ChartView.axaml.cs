using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.ChartDrawings;
using LT.Contracts.Dtos.Indicators;
using LT.Contracts.Dtos.PriceAlerts;
using LT.Contracts.Enums;
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
/// <para>
/// Price alerts: the menu's "Add alert at …" (or Alt+A while the chart has the keyboard) adds one
/// at the price under the pointer, or at the last price when the pointer is elsewhere; "Remove
/// alert at …" shows when the menu was opened on an alert line. Dragging a line and its ✕ are
/// handled by the chart and reach the view model here. The toolbox's alert tool: the chart
/// previews the line while it is armed and its click (<see cref="CandlestickChart.AlertPlaced"/>)
/// places the alert; Esc disarms it.
/// </para>
/// <para>
/// Orders: a dropped order line (<see cref="CandlestickChart.OrderMoved"/>) asks the view model to
/// confirm the new price; Esc puts a line being dragged, or one waiting for its confirmation, back.
/// </para>
/// <para>
/// Drawings: the chart raises a finished drawing, a moved one and a double-clicked note; they
/// reach the view model here. A note's text is typed in a box opened at the pointer (Enter or
/// clicking away keeps it, Esc drops it). Esc first drops a drawing being placed or moved, then
/// goes back to the cursor (or drops the selection); Delete removes the selected drawing. In
/// drawing mode Ctrl+Z undoes, Ctrl+Y / Ctrl+Shift+Z redoes, Ctrl+C copies the selected drawing,
/// Ctrl+V pastes the copied one and Ctrl+D duplicates the selected one (Cmd on macOS); a paste is
/// moved down by the chart's <see cref="CandlestickChart.GetPasteOffsetPrice"/>. Right-clicking a
/// drawing selects it and the menu offers copy, duplicate, lock, hide and delete; "Show hidden
/// drawings" brings back the ones hidden one by one.
/// </para>
/// <para>
/// Crosshair mode: F toggles it on the chart under the pointer, else on the chart that has the
/// keyboard (not while typing in a text box); Esc turns it off once nothing else is to be put
/// back. Its ruler goes on the next press anywhere in the window, when the window is
/// deactivated, or when the keyboard focus leaves the chart widget.
/// </para>
/// </summary>
public partial class ChartView : UserControl
{
	public ChartView()
	{
		InitializeComponent();
		Chart.IndicatorAction += OnIndicatorAction;
		Chart.AlertMoved += (id, price) => ViewModel?.MoveAlert(id, price);
		Chart.AlertRemoved += id => ViewModel?.RemoveAlert(id);
		Chart.AlertPlaced += price => ViewModel?.PlaceAlert(price);
		Chart.OrderMoved += (id, price) => ViewModel?.RequestOrderMove(id, price);
		Chart.OlderBarsNeeded += () => ViewModel?.LoadOlderBars();
		Chart.VisibleBarsChanged += (from, to) => ViewModel?.ShowFootprintBars(from, to);
		Chart.DrawingCreated += OnDrawingCreated;
		Chart.DrawingEdited += (id, points) => ViewModel?.UpdateDrawing(id, points);
		Chart.DrawingTextEditRequested += OnDrawingTextEditRequested;
		DataContextChanged += (_, _) => OnViewModelChanged();
		KeyDown += OnKeyDown;
		PropertyChanged += (_, e) =>
		{
			if (e.Property == IsKeyboardFocusWithinProperty && !IsKeyboardFocusWithin)
				Chart.ClearRuler();
		};
		ChartMenu.Opening += (_, _) => FillApplyTemplateMenu();
		ChartMenu.Opening += (_, _) => FillAlertMenu();
		ChartMenu.Opening += (_, _) => FillDrawingMenu();
		DuplicateDrawingMenuItem.Click += (_, _) => DuplicateDrawing();
		PasteDrawingMenuItem.Click += (_, _) => PasteDrawing();
		DuplicateDrawingButton.Click += (_, _) => DuplicateDrawing();
		AddAlertMenuItem.Click += (_, _) => ViewModel?.AddAlert(_menuAlertPrice);
		RemoveAlertMenuItem.Click += (_, _) =>
		{
			if (_menuAlert != null)
				ViewModel?.RemoveAlert(_menuAlert.Id);
		};
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

	// What the open menu's alert items act on: the price it was opened at, the alert under it
	private double? _menuAlertPrice;
	private PriceAlertDto? _menuAlert;

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

	// The window this chart is in, while attached: F over the chart and presses anywhere in it
	private TopLevel? _topLevel;

	protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
	{
		base.OnAttachedToVisualTree(e);
		_topLevel = TopLevel.GetTopLevel(this);
		if (_topLevel == null)
			return;
		_topLevel.AddHandler(KeyDownEvent, OnWindowKeyDown, RoutingStrategies.Tunnel);
		_topLevel.AddHandler(
			PointerPressedEvent,
			OnWindowPointerPressed,
			RoutingStrategies.Tunnel,
			handledEventsToo: true
		);
		if (_topLevel is Window window)
			window.Deactivated += OnWindowDeactivated;
	}

	protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
	{
		if (_topLevel != null)
		{
			_topLevel.RemoveHandler(KeyDownEvent, OnWindowKeyDown);
			_topLevel.RemoveHandler(PointerPressedEvent, OnWindowPointerPressed);
			if (_topLevel is Window window)
				window.Deactivated -= OnWindowDeactivated;
			_topLevel = null;
		}
		base.OnDetachedFromVisualTree(e);
	}

	// F with the pointer over this chart, wherever the keyboard is (before the focused control
	// sees it). Without the pointer over a chart, the chart with the keyboard takes it (OnKeyDown)
	private void OnWindowKeyDown(object? sender, KeyEventArgs e)
	{
		if (!IsCrosshairKey(e) || !IsPointerOver)
			return;
		e.Handled = true;
		Chart.IsCrosshairMode = !Chart.IsCrosshairMode;
	}

	// The ruler goes on any press in the window (one on the chart clears it there too)
	private void OnWindowPointerPressed(object? sender, PointerPressedEventArgs e) =>
		Chart.ClearRuler();

	private void OnWindowDeactivated(object? sender, EventArgs e) => Chart.ClearRuler();

	// Plain F, not while a text box has the keyboard
	private bool IsCrosshairKey(KeyEventArgs e) =>
		e.Key == Key.F
		&& e.KeyModifiers == KeyModifiers.None
		&& e.Source is not TextBox
		&& _topLevel?.FocusManager?.GetFocusedElement() is not TextBox;

	// The view model asks for the period's view (a period picked); only the current one is heard
	private ChartViewModel? _resetSource;

	private void OnViewModelChanged()
	{
		if (_resetSource != null)
			_resetSource.ViewResetRequested -= Chart.ResetView;
		_resetSource = ViewModel;
		if (_resetSource != null)
			_resetSource.ViewResetRequested += Chart.ResetView;
	}

	// Right-click: an alert at the price under the pointer (the last price off the price chart),
	// and the alert under it to remove
	private void FillAlertMenu()
	{
		var point = Chart.MenuPoint;
		_menuAlertPrice = (point is { } p ? Chart.GetPriceAt(p) : null) ?? ViewModel?.LastPrice;
		_menuAlert = point is { } q ? Chart.GetAlertAt(q) : null;
		AddAlertMenuItem.Header = _menuAlertPrice is { } price
			? $"Add alert at {PriceAlertHelpers.FormatPrice(price)}"
			: "Add alert";
		AddAlertMenuItem.IsEnabled = ViewModel?.CanAddAlert == true && _menuAlertPrice != null;
		RemoveAlertMenuItem.IsVisible = _menuAlert != null;
		if (_menuAlert != null)
			RemoveAlertMenuItem.Header =
				$"Remove alert at {PriceAlertHelpers.FormatPrice(_menuAlert.Price)}";
	}

	// Alt+A (TradingView's): an alert at the pointer's price, or the last price. Esc first puts
	// back an order line being dragged or waiting for its confirmation, then disarms the
	// toolbox's armed tool, then turns crosshair mode off. F toggles crosshair mode
	private void OnKeyDown(object? sender, KeyEventArgs e)
	{
		if (e.Key == Key.Escape && e.KeyModifiers == KeyModifiers.None)
		{
			if (
				Chart.CancelOrderDrag()
				|| ViewModel?.CancelOrderMove() == true
				|| Chart.CancelDrawing()
				|| ViewModel?.CancelTool() == true
				|| TurnOffCrosshair()
			)
				e.Handled = true;
			return;
		}
		if (IsCrosshairKey(e))
		{
			Chart.IsCrosshairMode = !Chart.IsCrosshairMode;
			e.Handled = true;
			return;
		}
		// Delete (Backspace on a Mac keyboard) removes the selected drawing; not while typing
		if (
			e.Key is Key.Delete or Key.Back
			&& e.KeyModifiers == KeyModifiers.None
			&& e.Source is not TextBox
			&& ViewModel is { IsDrawingMode: true, SelectedDrawingId: not null } drawingChart
		)
		{
			drawingChart.DeleteSelectedDrawing();
			e.Handled = true;
			return;
		}
		if (OnDrawingShortcut(e))
		{
			e.Handled = true;
			return;
		}
		if (e.Key != Key.A || e.KeyModifiers != KeyModifiers.Alt || ViewModel is not { } viewModel)
			return;
		e.Handled = true;
		viewModel.AddAlert(Chart.PointerPrice ?? viewModel.LastPrice);
	}

	private bool TurnOffCrosshair()
	{
		if (!Chart.IsCrosshairMode)
			return false;
		Chart.IsCrosshairMode = false;
		return true;
	}

	// Drawing mode, not while typing: undo / redo, copy / paste / duplicate. True when handled
	private bool OnDrawingShortcut(KeyEventArgs e)
	{
		var ctrl =
			e.KeyModifiers.HasFlag(KeyModifiers.Control)
			|| e.KeyModifiers.HasFlag(KeyModifiers.Meta);
		if (
			!ctrl
			|| e.KeyModifiers.HasFlag(KeyModifiers.Alt)
			|| e.Source is TextBox
			|| ViewModel is not { IsDrawingMode: true } viewModel
		)
			return false;
		var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
		switch (e.Key)
		{
			case Key.Z when shift:
			case Key.Y when !shift:
				Chart.CancelDrawing();
				viewModel.RedoDrawing();
				return true;
			case Key.Z:
				Chart.CancelDrawing();
				viewModel.UndoDrawing();
				return true;
			case Key.C when !shift && viewModel.SelectedDrawing != null:
				viewModel.CopySelectedDrawing();
				return true;
			case Key.V when !shift && viewModel.CanPasteDrawing:
				PasteDrawing();
				return true;
			case Key.D when !shift && viewModel.SelectedDrawing != null:
				DuplicateDrawing();
				return true;
			default:
				return false;
		}
	}

	private void PasteDrawing()
	{
		Chart.CancelDrawing();
		ViewModel?.PasteDrawing(Chart.GetPasteOffsetPrice());
	}

	private void DuplicateDrawing() =>
		ViewModel?.DuplicateSelectedDrawing(Chart.GetPasteOffsetPrice());

	// Right-click in drawing mode: the drawing under the pointer is selected and acted on; paste
	// and the hidden drawings show when there are any
	private void FillDrawingMenu()
	{
		var viewModel = ViewModel;
		var drawing =
			viewModel is { IsDrawingMode: true } && Chart.MenuPoint is { } point
				? Chart.GetDrawingAt(point)
				: null;
		if (drawing != null)
			viewModel!.SelectedDrawingId = drawing.Id;
		var onDrawing = drawing != null;
		CopyDrawingMenuItem.IsVisible = onDrawing;
		DuplicateDrawingMenuItem.IsVisible = onDrawing;
		LockDrawingMenuItem.IsVisible = onDrawing;
		HideDrawingMenuItem.IsVisible = onDrawing;
		DeleteDrawingMenuItem.IsVisible = onDrawing;
		if (drawing != null)
		{
			LockDrawingMenuItem.Header = drawing.IsLocked ? "Unlock drawing" : "Lock drawing";
			DeleteDrawingMenuItem.IsEnabled = !drawing.IsLocked;
		}
		PasteDrawingMenuItem.IsVisible =
			viewModel is { IsDrawingMode: true, CanPasteDrawing: true };
		ShowHiddenDrawingsMenuItem.IsVisible = viewModel?.HasHiddenDrawings == true;
		if (viewModel?.HiddenDrawingCount is > 0 and var hidden)
			ShowHiddenDrawingsMenuItem.Header =
				$"Show {hidden} hidden drawing{(hidden == 1 ? "" : "s")}";
		DrawingMenuSeparator.IsVisible =
			onDrawing || PasteDrawingMenuItem.IsVisible || ShowHiddenDrawingsMenuItem.IsVisible;
	}

	// A note asks for its text first; every other drawing is added as drawn
	private void OnDrawingCreated(ChartDrawingKind kind, IReadOnlyList<ChartDrawingPointDto> points)
	{
		if (ViewModel is not { } viewModel)
			return;
		if (kind != ChartDrawingKind.Text)
		{
			viewModel.AddDrawing(kind, points);
			return;
		}
		viewModel.EndDrawingTool();
		EditDrawingText(null, text => viewModel.AddDrawing(kind, points, text));
	}

	private void OnDrawingTextEditRequested(string id)
	{
		if (ViewModel is not { } viewModel || viewModel.GetDrawing(id) is not { } drawing)
			return;
		EditDrawingText(drawing.Text, text => viewModel.UpdateDrawing(id, drawing.Points, text));
	}

	// A box at the pointer for a note's text: Enter or clicking away keeps it (when not empty),
	// Esc drops it
	private void EditDrawingText(string? text, Action<string> commit)
	{
		var box = new TextBox
		{
			Width = 260,
			Text = text,
			PlaceholderText = "Note (Enter to place)",
			MaxLength = ChartDrawingConstants.MaxTextLength,
		};
		var flyout = new Flyout { Content = box };
		var finished = false;
		void Finish(bool keep)
		{
			if (finished)
				return;
			finished = true;
			if (keep && !string.IsNullOrWhiteSpace(box.Text))
				commit(box.Text.Trim());
		}
		box.KeyDown += (_, e) =>
		{
			if (e.Key is not (Key.Enter or Key.Escape))
				return;
			e.Handled = true;
			Finish(e.Key == Key.Enter);
			flyout.Hide();
		};
		flyout.Opened += (_, _) =>
			Dispatcher.UIThread.Post(
				() =>
				{
					box.Focus();
					box.SelectAll();
				},
				DispatcherPriority.Loaded
			);
		flyout.Closed += (_, _) =>
		{
			Finish(true);
			Chart.Focus();
		};
		flyout.ShowAt(Chart, showAtPointer: true);
	}

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
