using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LSCore.Exceptions;
using LT.Contracts.Dtos.ChartDrawings;
using LT.Contracts.Enums;
using LT.Contracts.Interfaces.IManagers;
using LT.Contracts.Requests.ChartDrawings;

namespace LT.App.Widgets.Chart;

/// <summary>
/// Drawings on the chart (<see cref="IChartDrawingManager"/>, kept per contract: every chart of a
/// symbol shows the same ones). The toolbox bar's Draw button turns drawing mode on (saved per
/// chart): a palette of tools appears at the plot's left. A tool is armed by its button and draws
/// with the next clicks (or a drag) on the price chart, then the cursor is armed again and the new
/// drawing selected. With the cursor, a click selects a drawing and dragging moves it or one of
/// its anchors; Delete removes the selected one. The palette's actions delete the selected one,
/// remove them all (asked first), lock them (no selecting, moving or deleting one by one) and
/// hide them; lock and hide are saved per chart. With drawing mode off the drawings are still
/// shown, but the chart pans, zooms and places alerts as it does without them.
/// </summary>
public partial class ChartViewModel
{
	private const string DrawingModeIcon = "M4,20 L4,16 L16,4 L20,8 L8,20 Z M13,7 L17,11";

	private readonly IChartDrawingManager _chartDrawingManager;
	private int _drawingsQueued;

	// The palette's actions, set when it is built; their state follows the selection and drawings
	private (
		ChartDrawingToolItem Delete,
		ChartDrawingToolItem RemoveAll,
		ChartDrawingToolItem Lock,
		ChartDrawingToolItem Hide
	)? _drawingActions;

	/// <summary>The charted contract's drawings, oldest first.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(HasDrawings), nameof(RemoveAllDrawingsPrompt))]
	public partial IReadOnlyList<ChartDrawingDto> Drawings { get; set; } = [];

	public bool HasDrawings => Drawings.Count > 0;

	/// <summary>Drawing mode: the palette at the plot's left, drawings selectable. Saved per chart.</summary>
	[ObservableProperty]
	public partial bool IsDrawingMode { get; set; }

	/// <summary>The armed drawing tool; null is the cursor (select and move drawings).</summary>
	[ObservableProperty]
	public partial ChartDrawingKind? ActiveDrawingKind { get; set; }

	/// <summary>The drawing selected with the cursor (Delete removes it); null when none is.</summary>
	[ObservableProperty]
	public partial string? SelectedDrawingId { get; set; }

	/// <summary>Drawings can't be selected, moved or deleted one by one. Saved per chart.</summary>
	[ObservableProperty]
	public partial bool DrawingsLocked { get; set; }

	/// <summary>Drawings aren't drawn. Saved per chart; arming a tool shows them again.</summary>
	[ObservableProperty]
	public partial bool DrawingsHidden { get; set; }

	/// <summary>"Remove all drawings" asks first: the prompt is shown over the plot.</summary>
	[ObservableProperty]
	public partial bool IsConfirmingRemoveAllDrawings { get; set; }

	public string RemoveAllDrawingsPrompt =>
		$"Remove all {Drawings.Count} drawing{(Drawings.Count == 1 ? "" : "s")} of {_contract?.Symbol ?? "this symbol"}?";

	/// <summary>The Draw button of the toolbox bar.</summary>
	public ChartDrawingToolItem DrawingModeItem =>
		field ??= new(
			"Draw",
			"Drawing mode: trend lines, shapes, Fibonacci, measures, notes (tools at the chart's left)",
			DrawingModeIcon,
			ToggleDrawingModeCommand
		);

	/// <summary>The palette, in groups.</summary>
	public IReadOnlyList<ChartDrawingToolGroup> DrawingToolGroups => field ??= CreateDrawingTools();

	private IEnumerable<ChartDrawingToolItem> DrawingToolItems =>
		DrawingToolGroups.SelectMany(x => x.Items);

	/// <summary>The toolbox's Draw button: turns drawing mode on or off.</summary>
	[RelayCommand]
	public void ToggleDrawingMode() => IsDrawingMode = !IsDrawingMode;

	/// <summary>A palette tool: arms it, or the cursor when it is the armed one.</summary>
	[RelayCommand]
	public void SelectDrawingTool(ChartDrawingToolItem? item)
	{
		if (item is not { IsTool: true } || !IsDrawingMode)
			return;
		ActiveDrawingKind = item.Kind == ActiveDrawingKind || !CanDraw ? null : item.Kind;
	}

	/// <summary>Back to the cursor (a drawing was finished or a note cancelled).</summary>
	public void EndDrawingTool() => ActiveDrawingKind = null;

	/// <summary>Drawings can be added: a contract with bars (to anchor them on).</summary>
	public bool CanDraw => _contract != null && Bars.Count > 0;

	/// <summary>
	/// A drawing was finished on the chart: adds it, selects it and arms the cursor again. Null
	/// when refused (the reason shows on the chart).
	/// </summary>
	public ChartDrawingDto? AddDrawing(
		ChartDrawingKind kind,
		IReadOnlyList<ChartDrawingPointDto> points,
		string? text = null
	)
	{
		ActiveDrawingKind = null;
		if (_contract is not { } contract || !CanDraw)
			return null;
		try
		{
			var created = _chartDrawingManager.Create(
				new CreateChartDrawingRequest
				{
					ConId = contract.ConId,
					Kind = kind,
					Points = [.. points],
					Text = text,
				}
			);
			LoadDrawings();
			SelectedDrawingId = created.Id;
			return created;
		}
		catch (LSCoreBadRequestException ex)
		{
			ErrorMessage = ex.Message;
			return null;
		}
	}

	/// <summary>A drawing's anchors were dragged (or its note edited; null keeps it).</summary>
	public void UpdateDrawing(
		string id,
		IReadOnlyList<ChartDrawingPointDto> points,
		string? text = null
	)
	{
		try
		{
			_chartDrawingManager.Update(
				new UpdateChartDrawingRequest
				{
					Id = id,
					Points = [.. points],
					Text = text,
				}
			);
		}
		catch (LSCoreBadRequestException ex)
		{
			ErrorMessage = ex.Message;
		}
		finally
		{
			// Now: the drawing stays where it was dropped, or a refused one goes back at once
			LoadDrawings();
		}
	}

	/// <summary>A drawing by id, among the charted contract's.</summary>
	public ChartDrawingDto? GetDrawing(string id) => Drawings.FirstOrDefault(x => x.Id == id);

	/// <summary>Palette and Delete: removes the selected drawing (not while locked).</summary>
	[RelayCommand]
	public void DeleteSelectedDrawing()
	{
		if (SelectedDrawingId is not { } id || DrawingsLocked)
			return;
		SelectedDrawingId = null;
		_chartDrawingManager.Delete(id);
		LoadDrawings();
	}

	/// <summary>Palette: asks before removing every drawing of the symbol.</summary>
	[RelayCommand]
	public void AskRemoveAllDrawings()
	{
		if (HasDrawings)
			IsConfirmingRemoveAllDrawings = true;
	}

	[RelayCommand]
	public void ConfirmRemoveAllDrawings()
	{
		IsConfirmingRemoveAllDrawings = false;
		SelectedDrawingId = null;
		if (_contract is { } contract)
			_chartDrawingManager.DeleteAll(contract.ConId);
		LoadDrawings();
	}

	[RelayCommand]
	public void CancelRemoveAllDrawings() => IsConfirmingRemoveAllDrawings = false;

	[RelayCommand]
	public void ToggleDrawingsLocked() => DrawingsLocked = !DrawingsLocked;

	[RelayCommand]
	public void ToggleDrawingsHidden() => DrawingsHidden = !DrawingsHidden;

	/// <summary>
	/// Esc after the chart's own (an unfinished drawing): back to the cursor, else the selection
	/// is dropped. False when there was nothing to undo.
	/// </summary>
	private bool CancelDrawingTool()
	{
		if (ActiveDrawingKind != null)
		{
			ActiveDrawingKind = null;
			return true;
		}
		if (IsConfirmingRemoveAllDrawings)
		{
			IsConfirmingRemoveAllDrawings = false;
			return true;
		}
		if (SelectedDrawingId != null)
		{
			SelectedDrawingId = null;
			return true;
		}
		return false;
	}

	partial void OnIsDrawingModeChanged(bool value)
	{
		DrawingModeItem.IsActive = value;
		if (value)
			return;
		ActiveDrawingKind = null;
		SelectedDrawingId = null;
		IsConfirmingRemoveAllDrawings = false;
	}

	partial void OnActiveDrawingKindChanged(ChartDrawingKind? value)
	{
		foreach (var item in DrawingToolItems.Where(x => x.IsTool))
			item.IsActive = item.Kind == value;
		if (value == null)
			return;
		// One armed tool at a time; a new drawing shows with the others
		ActiveTool = null;
		SelectedDrawingId = null;
		DrawingsHidden = false;
	}

	partial void OnSelectedDrawingIdChanged(string? value) => RefreshDrawingActions();

	partial void OnDrawingsLockedChanged(bool value)
	{
		if (value)
			SelectedDrawingId = null;
		RefreshDrawingActions();
	}

	partial void OnDrawingsHiddenChanged(bool value)
	{
		if (value)
			SelectedDrawingId = null;
		RefreshDrawingActions();
	}

	partial void OnDrawingsChanged(IReadOnlyList<ChartDrawingDto> value)
	{
		if (SelectedDrawingId is { } id && value.All(x => x.Id != id))
			SelectedDrawingId = null;
		if (value.Count == 0)
			IsConfirmingRemoveAllDrawings = false;
		RefreshDrawingActions();
	}

	// From any thread (another chart of the contract changed one); one UI update per burst
	private void OnDrawingsChangedElsewhere()
	{
		if (Interlocked.Exchange(ref _drawingsQueued, 1) == 0)
			Dispatcher.UIThread.Post(() =>
			{
				Interlocked.Exchange(ref _drawingsQueued, 0);
				if (!_disposed)
					LoadDrawings();
			});
	}

	private void LoadDrawings() =>
		Drawings = _contract is { } contract
			? _chartDrawingManager.GetDrawings(contract.ConId)
			: [];

	// The contract or bars changed: the selection and an armed tool may no longer apply
	private void RefreshDrawingTools()
	{
		var canDraw = CanDraw;
		foreach (var item in DrawingToolItems.Where(x => x.IsTool && x.Kind != null))
			item.IsEnabled = canDraw;
		if (!canDraw)
			ActiveDrawingKind = null;
		RefreshDrawingActions();
	}

	private void RefreshDrawingActions()
	{
		if (_drawingActions is not { } actions)
			return;
		actions.Delete.IsEnabled = SelectedDrawingId != null && !DrawingsLocked;
		actions.RemoveAll.IsEnabled = HasDrawings;
		actions.Lock.IsActive = DrawingsLocked;
		actions.Lock.ToolTip = DrawingsLocked
			? "Unlock drawings: select, move and delete them again"
			: "Lock drawings: no selecting, moving or deleting them one by one";
		actions.Hide.IsActive = DrawingsHidden;
		actions.Hide.ToolTip = DrawingsHidden ? "Show drawings" : "Hide drawings";
	}

	private List<ChartDrawingToolGroup> CreateDrawingTools()
	{
		ChartDrawingToolItem Tool(
			string label,
			string toolTip,
			string icon,
			ChartDrawingKind? kind
		) =>
			new(label, toolTip, icon, SelectDrawingToolCommand, kind)
			{
				IsTool = true,
				IsActive = kind == ActiveDrawingKind,
				IsEnabled = kind == null || CanDraw,
			};
		var delete = new ChartDrawingToolItem(
			"Delete",
			"Delete the selected drawing (Delete)",
			"M5,7 H19 M10,7 V4 H14 V7 M7,7 L8,20 H16 L17,7 M10,11 V16 M14,11 V16",
			DeleteSelectedDrawingCommand
		);
		var removeAll = new ChartDrawingToolItem(
			"Remove all",
			"Remove all drawings of this symbol",
			"M5,7 H19 M10,7 V4 H14 V7 M7,7 L8,20 H16 L17,7 M10,11 L14,16 M14,11 L10,16",
			AskRemoveAllDrawingsCommand
		);
		var lockItem = new ChartDrawingToolItem(
			"Lock",
			"",
			"M6,11 H18 V21 H6 Z M8,11 V8 A4,4 0 0,1 16,8 V11",
			ToggleDrawingsLockedCommand
		);
		var hide = new ChartDrawingToolItem(
			"Hide",
			"",
			"M2,12 C5,6 19,6 22,12 C19,18 5,18 2,12 Z M9,12 A3,3 0 1,0 15,12 A3,3 0 1,0 9,12 M4,4 L20,20",
			ToggleDrawingsHiddenCommand
		);
		_drawingActions = (delete, removeAll, lockItem, hide);
		List<ChartDrawingToolGroup> groups =
		[
			new([
				Tool(
					"Cursor",
					"Cursor: select a drawing, drag it or its points to edit (Esc)",
					"M7,3 L7,19 L11,15 L14,21 L16,20 L13,14 L18,14 Z",
					null
				),
			]),
			new([
				Tool(
					"Trend line",
					"Trend line: click two points (or drag)",
					"M7.4,16.6 L16.6,7.4 M4,18 A2,2 0 1,0 8,18 A2,2 0 1,0 4,18 M16,6 A2,2 0 1,0 20,6 A2,2 0 1,0 16,6",
					ChartDrawingKind.TrendLine
				),
				Tool(
					"Ray",
					"Ray: from the first point through the second, on to the edge",
					"M7.4,16.6 L22,2 M4,18 A2,2 0 1,0 8,18 A2,2 0 1,0 4,18 M13,11 A2,2 0 1,0 17,11 A2,2 0 1,0 13,11",
					ChartDrawingKind.Ray
				),
				Tool(
					"Extended line",
					"Extended line: through two points, to both edges",
					"M2,22 L22,2 M6,16 A2,2 0 1,0 10,16 A2,2 0 1,0 6,16 M14,8 A2,2 0 1,0 18,8 A2,2 0 1,0 14,8",
					ChartDrawingKind.ExtendedLine
				),
				Tool(
					"Horizontal line",
					"Horizontal line: click a price",
					"M2,12 L10,12 M14,12 L22,12 M10,12 A2,2 0 1,0 14,12 A2,2 0 1,0 10,12",
					ChartDrawingKind.HorizontalLine
				),
				Tool(
					"Horizontal ray",
					"Horizontal ray: from the clicked point to the right",
					"M8,12 L22,12 M4,12 A2,2 0 1,0 8,12 A2,2 0 1,0 4,12",
					ChartDrawingKind.HorizontalRay
				),
				Tool(
					"Vertical line",
					"Vertical line: click a time",
					"M12,2 L12,10 M12,14 L12,22 M10,12 A2,2 0 1,0 14,12 A2,2 0 1,0 10,12",
					ChartDrawingKind.VerticalLine
				),
				Tool(
					"Arrow",
					"Arrow: click where it starts, then where it points",
					"M5,19 L19,5 M11,5 L19,5 L19,13",
					ChartDrawingKind.Arrow
				),
			]),
			new([
				Tool(
					"Parallel channel",
					"Parallel channel: two points for the trend line, a third for its parallel",
					"M3,15 L15,3 M9,21 L21,9",
					ChartDrawingKind.ParallelChannel
				),
			]),
			new([
				Tool(
					"Rectangle",
					"Rectangle: two opposite corners",
					"M4,6 H20 V18 H4 Z",
					ChartDrawingKind.Rectangle
				),
				Tool(
					"Ellipse",
					"Ellipse: two opposite corners of its box",
					"M2,12 A10,7 0 1,0 22,12 A10,7 0 1,0 2,12",
					ChartDrawingKind.Ellipse
				),
				Tool(
					"Triangle",
					"Triangle: click three corners",
					"M12,4 L21,20 L3,20 Z",
					ChartDrawingKind.Triangle
				),
			]),
			new([
				Tool(
					"Fib retracement",
					"Fibonacci retracement: from the start of the move to its end",
					"M3,4 H21 M3,9 H21 M3,14 H21 M3,20 H21 M5,20 L19,4",
					ChartDrawingKind.FibRetracement
				),
				Tool(
					"Fib extension",
					"Trend-based Fibonacci extension: the move's start, its end, then where the next leg starts",
					"M2,20 L7,8 L11,15 M13,4 H22 M13,9 H22 M13,14 H22 M13,19 H22",
					ChartDrawingKind.FibExtension
				),
			]),
			new([
				Tool(
					"Price range",
					"Price range: measure the change between two prices",
					"M4,3 H20 M4,21 H20 M12,6 V18 M9,9 L12,6 L15,9 M9,15 L12,18 L15,15",
					ChartDrawingKind.PriceRange
				),
				Tool(
					"Date range",
					"Date range: measure the bars and time between two points",
					"M3,4 V20 M21,4 V20 M6,12 H18 M9,9 L6,12 L9,15 M15,9 L18,12 L15,15",
					ChartDrawingKind.DateRange
				),
			]),
			new([
				Tool(
					"Text",
					"Text: click where the note goes, type it, Enter",
					"M5,5 H19 M12,5 V20 M9,20 H15",
					ChartDrawingKind.Text
				),
				Tool(
					"Brush",
					"Brush: draw freehand while the button is held",
					"M3,17 C6,10 9,20 12,13 S18,6 21,9",
					ChartDrawingKind.Brush
				),
			]),
			new([delete, removeAll, lockItem, hide]),
		];
		RefreshDrawingActions();
		return groups;
	}
}
