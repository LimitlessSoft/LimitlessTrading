using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LSCore.Exceptions;
using LT.App.Helpers;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.ChartDrawings;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;
using LT.Contracts.Requests.ChartDrawings;

namespace LT.App.Widgets.Chart;

/// <summary>
/// The selected drawing's style bar (floating over the plot in drawing mode): its colour (a swatch
/// or a typed hex), line width and style, its Fibonacci levels, lock, hide, duplicate and delete.
/// A new style also becomes the kind's default for the next drawings. Copy / paste go through the
/// app's <see cref="LT.App.Stores.ChartDrawingClipboardStore"/>, so a drawing copied on one chart
/// can be pasted on another (a paste lands a little right of and below the copied one, and the
/// next paste lands below that). Every change is one undo step.
/// </summary>
public partial class ChartViewModel
{
	/// <summary>The selected drawing as last loaded; null when none is.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(
		nameof(SelectedDrawingBrush),
		nameof(SelectedDrawingHasColor),
		nameof(SelectedDrawingHasLineStyle),
		nameof(SelectedDrawingIsFib),
		nameof(SelectedDrawingLocked),
		nameof(SelectedDrawingLockToolTip)
	)]
	public partial ChartDrawingDto? SelectedDrawing { get; set; }

	/// <summary>The style bar shows: drawing mode, a drawing selected, the cursor armed, not all locked.</summary>
	[ObservableProperty]
	public partial bool IsStyleBarVisible { get; set; }

	/// <summary>The style bar's hex box (Enter or Set applies it).</summary>
	[ObservableProperty]
	public partial string? SelectedDrawingColorText { get; set; }

	public IBrush? SelectedDrawingBrush =>
		ChartDrawingColorItem.ToBrush(SelectedDrawing?.Style.Color);

	public bool SelectedDrawingHasColor =>
		SelectedDrawing is { } d && ChartDrawingHelpers.HasColor(d.Kind);

	public bool SelectedDrawingHasLineStyle =>
		SelectedDrawing is { } d && ChartDrawingHelpers.HasLineStyle(d.Kind);

	public bool SelectedDrawingIsFib =>
		SelectedDrawing is { } d && ChartDrawingHelpers.IsFib(d.Kind);

	public bool SelectedDrawingLocked => SelectedDrawing?.IsLocked == true;

	public string SelectedDrawingLockToolTip =>
		SelectedDrawingLocked
			? "Unlock: move, reshape and delete it again"
			: "Lock: no moving, reshaping or deleting it";

	/// <summary>The style bar's swatches.</summary>
	public IReadOnlyList<ChartDrawingColorItem> DrawingColors =>
		field ??= [
			.. ChartDrawingConstants.Colors.Select(x => new ChartDrawingColorItem(
				x,
				SetSelectedDrawingColorCommand
			)),
		];

	/// <summary>The style bar's widths, each a line in the selected drawing's style.</summary>
	public IReadOnlyList<ChartDrawingLineOption> DrawingLineWidths =>
		field ??= [
			.. ChartDrawingConstants.LineWidths.Select(x => new ChartDrawingLineOption(
				$"{x.ToString("0.#", CultureInfo.CurrentCulture)} px",
				x,
				ChartDrawingLineStyle.Solid,
				SetSelectedDrawingLineWidthCommand
			)),
		];

	public IReadOnlyList<ChartDrawingLineOption> DrawingLineStyles =>
		field ??= [
			.. Enum.GetValues<ChartDrawingLineStyle>()
				.Select(x => new ChartDrawingLineOption(
					x.ToString(),
					2,
					x,
					SetSelectedDrawingLineStyleCommand
				)),
		];

	/// <summary>The Fibonacci levels editor's rows (a working copy until Apply).</summary>
	public ObservableCollection<ChartFibLevelItem> FibLevelItems { get; } = [];

	[ObservableProperty]
	public partial string? NewFibLevelText { get; set; }

	/// <summary>Why the editor's levels can't be applied; null when they can.</summary>
	[ObservableProperty]
	public partial string? FibLevelsError { get; set; }

	// ---- style -------------------------------------------------------------------------------

	/// <summary>A swatch: the selected drawing's colour.</summary>
	[RelayCommand]
	public void SetSelectedDrawingColor(string? color)
	{
		if (color == null || SelectedDrawing is not { } drawing)
			return;
		UpdateSelectedDrawing(
			new UpdateChartDrawingPropertiesRequest
			{
				Id = drawing.Id,
				Style = WithStyle(drawing.Style, color: color),
			}
		);
	}

	/// <summary>The hex box: the selected drawing's colour, when it is one ("#2962FF").</summary>
	[RelayCommand]
	public void ApplySelectedDrawingColorText()
	{
		if (
			SelectedDrawing is not { } drawing
			|| string.IsNullOrWhiteSpace(SelectedDrawingColorText)
		)
			return;
		UpdateSelectedDrawing(
			new UpdateChartDrawingPropertiesRequest
			{
				Id = drawing.Id,
				Style = WithStyle(drawing.Style, color: SelectedDrawingColorText.Trim()),
			}
		);
	}

	[RelayCommand]
	public void SetSelectedDrawingLineWidth(ChartDrawingLineOption? option)
	{
		if (option == null || SelectedDrawing is not { } drawing)
			return;
		UpdateSelectedDrawing(
			new UpdateChartDrawingPropertiesRequest
			{
				Id = drawing.Id,
				Style = WithStyle(drawing.Style, width: option.Width),
			}
		);
	}

	[RelayCommand]
	public void SetSelectedDrawingLineStyle(ChartDrawingLineOption? option)
	{
		if (option == null || SelectedDrawing is not { } drawing)
			return;
		UpdateSelectedDrawing(
			new UpdateChartDrawingPropertiesRequest
			{
				Id = drawing.Id,
				Style = WithStyle(drawing.Style, lineStyle: option.LineStyle),
			}
		);
	}

	/// <summary>The style bar's lock and the menu's: locks or unlocks the selected drawing.</summary>
	[RelayCommand]
	public void ToggleSelectedDrawingLocked()
	{
		if (SelectedDrawing is not { } drawing)
			return;
		UpdateSelectedDrawing(
			new UpdateChartDrawingPropertiesRequest
			{
				Id = drawing.Id,
				IsLocked = !drawing.IsLocked,
			}
		);
	}

	/// <summary>Hides the selected drawing (and drops the selection); the menu shows it again.</summary>
	[RelayCommand]
	public void HideSelectedDrawing()
	{
		if (SelectedDrawing is not { } drawing)
			return;
		UpdateSelectedDrawing(
			new UpdateChartDrawingPropertiesRequest { Id = drawing.Id, IsHidden = true }
		);
	}

	/// <summary>The menu: shows the drawings hidden one by one again (also the global hide).</summary>
	[RelayCommand]
	public void ShowHiddenDrawings()
	{
		DrawingsHidden = false;
		if (_contract is { } contract && _chartDrawingManager.ShowHidden(contract.ConId) > 0)
			LoadDrawings();
	}

	// ---- copy / paste --------------------------------------------------------------------

	/// <summary>Ctrl+C: the selected drawing, for a paste on any chart.</summary>
	[RelayCommand]
	public void CopySelectedDrawing()
	{
		if (SelectedDrawing is { } drawing)
			_drawingClipboard.Drawing = drawing;
	}

	/// <summary>A copied drawing waits to be pasted.</summary>
	public bool CanPasteDrawing => _drawingClipboard.Drawing != null && CanDraw;

	/// <summary>
	/// Ctrl+V: a copy of the copied drawing on this chart's symbol, <see
	/// cref="ChartDrawingConstants.PasteOffsetBars"/> bars on and <paramref name="priceShift"/>
	/// (the chart's <see cref="ChartDrawingConstants.PasteOffsetDips"/> in price, negative: down)
	/// off; it is selected, and the next paste goes on from it.
	/// </summary>
	public ChartDrawingDto? PasteDrawing(double priceShift)
	{
		if (_drawingClipboard.Drawing is not { } copied)
			return null;
		var pasted = AddCopy(copied, priceShift);
		if (pasted != null)
			_drawingClipboard.Drawing = pasted;
		return pasted;
	}

	/// <summary>The style bar's duplicate (Ctrl+D): a copy of the selected drawing next to it.</summary>
	public ChartDrawingDto? DuplicateSelectedDrawing(double priceShift) =>
		SelectedDrawing is { } drawing ? AddCopy(drawing, priceShift) : null;

	private ChartDrawingDto? AddCopy(ChartDrawingDto source, double priceShift)
	{
		if (_contract is not { } contract || !CanDraw)
			return null;
		ActiveDrawingKind = null;
		try
		{
			var created = _chartDrawingManager.Create(
				new CreateChartDrawingRequest
				{
					ConId = contract.ConId,
					Kind = source.Kind,
					Points = ChartDrawingHelpers.OffsetPoints(
						source.Points,
						Bars,
						ChartTimeframeHelpers.GetBarLength(SelectedTimeframe.Value),
						ChartDrawingConstants.PasteOffsetBars,
						priceShift
					),
					Text = source.Text,
					Style = source.Style,
					FibLevels = ChartDrawingHelpers.IsFib(source.Kind) ? source.FibLevels : null,
				}
			);
			LoadDrawings();
			SelectedDrawingId = created.Id;
			return created;
		}
		catch (LSCoreBadRequestException ex)
		{
			ErrorMessage = ErrorMessageHelpers.ToUserMessage(ex);
			return null;
		}
	}

	// ---- Fibonacci levels ----------------------------------------------------------------

	/// <summary>The levels editor opens: its rows are the selected drawing's levels.</summary>
	[RelayCommand]
	public void BeginEditFibLevels()
	{
		FibLevelItems.Clear();
		FibLevelsError = null;
		NewFibLevelText = null;
		if (SelectedDrawing is not { } drawing || !ChartDrawingHelpers.IsFib(drawing.Kind))
			return;
		foreach (var level in drawing.FibLevels)
			FibLevelItems.Add(ToItem(level));
	}

	/// <summary>The editor's add box: a new level (in the drawing's colour), kept in order.</summary>
	[RelayCommand]
	public void AddFibLevel()
	{
		if (!ChartDrawingHelpers.TryParseFibLevel(NewFibLevelText, out var value))
		{
			FibLevelsError = "Type a level, like 0.65 or 65%.";
			return;
		}
		var index = FibLevelItems
			.TakeWhile(x =>
				!ChartDrawingHelpers.TryParseFibLevel(x.ValueText, out var other) || other < value
			)
			.Count();
		FibLevelItems.Insert(index, new ChartFibLevelItem(this, FormatLevel(value), true, null));
		NewFibLevelText = null;
		FibLevelsError = null;
	}

	[RelayCommand]
	public void RemoveFibLevel(ChartFibLevelItem? item)
	{
		if (item != null)
			FibLevelItems.Remove(item);
	}

	/// <summary>The editor's rows back to the kind's default levels (applied with Apply).</summary>
	[RelayCommand]
	public void ResetFibLevels()
	{
		if (SelectedDrawing is not { } drawing)
			return;
		FibLevelItems.Clear();
		foreach (var level in ChartDrawingHelpers.GetDefaultFibLevels(drawing.Kind))
			FibLevelItems.Add(ToItem(level));
		FibLevelsError = null;
	}

	/// <summary>The editor's rows become the drawing's levels (one undo step). False when refused.</summary>
	public bool ApplyFibLevels()
	{
		if (SelectedDrawing is not { } drawing)
			return false;
		var levels = new List<ChartDrawingFibLevelDto>();
		foreach (var item in FibLevelItems)
		{
			if (!ChartDrawingHelpers.TryParseFibLevel(item.ValueText, out var value))
			{
				FibLevelsError = $"\"{item.ValueText}\" isn't a level.";
				return false;
			}
			levels.Add(
				new ChartDrawingFibLevelDto
				{
					Value = value,
					IsVisible = item.IsVisible,
					Color = item.SelectedColor.Hex,
				}
			);
		}
		try
		{
			_chartDrawingManager.UpdateProperties(
				new UpdateChartDrawingPropertiesRequest { Id = drawing.Id, FibLevels = levels }
			);
			FibLevelsError = null;
			return true;
		}
		catch (LSCoreBadRequestException ex)
		{
			FibLevelsError = ErrorMessageHelpers.ToUserMessage(ex);
			return false;
		}
		finally
		{
			LoadDrawings();
		}
	}

	// The editor's Apply (it stays open, the chart showing the new levels)
	[RelayCommand]
	private void ApplyFibLevelsEdit() => ApplyFibLevels();

	private ChartFibLevelItem ToItem(ChartDrawingFibLevelDto level) =>
		new(this, FormatLevel(level.Value), level.IsVisible, level.Color);

	private static string FormatLevel(double value) =>
		value.ToString("0.####", CultureInfo.InvariantCulture);

	// ---- shared --------------------------------------------------------------------------

	private static ChartDrawingStyleDto WithStyle(
		ChartDrawingStyleDto style,
		string? color = null,
		double? width = null,
		ChartDrawingLineStyle? lineStyle = null
	) =>
		new()
		{
			Color = color ?? style.Color,
			LineWidth = width ?? style.LineWidth,
			LineStyle = lineStyle ?? style.LineStyle,
		};

	private void UpdateSelectedDrawing(UpdateChartDrawingPropertiesRequest request)
	{
		try
		{
			_chartDrawingManager.UpdateProperties(request);
		}
		catch (LSCoreBadRequestException ex)
		{
			ErrorMessage = ErrorMessageHelpers.ToUserMessage(ex);
		}
		finally
		{
			LoadDrawings();
		}
	}

	// The selection or the drawings changed: the style bar follows the selected one
	private void RefreshSelectedDrawing()
	{
		var drawing = SelectedDrawingId is { } id ? Drawings.FirstOrDefault(x => x.Id == id) : null;
		SelectedDrawing = drawing;
		SelectedDrawingColorText = drawing?.Style.Color;
		IsStyleBarVisible =
			drawing != null
			&& IsDrawingMode
			&& ActiveDrawingKind == null
			&& !DrawingsLocked
			&& !DrawingsHidden;
		foreach (var option in DrawingLineWidths)
			option.IsActive =
				drawing != null && Math.Abs(option.Width - drawing.Style.LineWidth) < 0.01;
		foreach (var option in DrawingLineStyles)
			option.IsActive = drawing != null && option.LineStyle == drawing.Style.LineStyle;
	}
}
