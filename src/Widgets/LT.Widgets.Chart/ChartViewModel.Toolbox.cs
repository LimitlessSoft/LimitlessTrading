using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace LT.App.Widgets.Chart;

/// <summary>
/// The toolbox bar above the plot: each chart shows or hides it (right-click → Show toolbox, or
/// its ✕), saved with the chart; shown by default. A tool is armed by its button and used by the
/// next click on the chart; clicking the button again or Esc disarms it. The alert tool shows a
/// preview line at the pointer's price and adds an alert where the price chart is clicked (like
/// Add alert, the direction told from the last close). The bar's Draw button turns drawing mode
/// on and off (ChartViewModel.Drawings.cs); an alert tool and a drawing tool are never armed
/// together.
/// </summary>
public partial class ChartViewModel
{
	private const string AlertToolIcon =
		"M21,19V20H3V19L5,17V11C5,7.9 7.03,5.17 10,4.29C10,4.19 10,4.1 10,4A2,2 0 0,1 12,2A2,2 0 0,1 14,4C14,4.1 14,4.19 14,4.29C16.97,5.17 19,7.9 19,11V17L21,19M14,21A2,2 0 0,1 12,23A2,2 0 0,1 10,21";

	/// <summary>The toolbox bar is shown; each chart has its own, saved with it.</summary>
	[ObservableProperty]
	public partial bool ShowToolbox { get; set; } = true;

	/// <summary>The toolbox bar's buttons, in order.</summary>
	public IReadOnlyList<ChartToolItem> Tools => field ??= CreateTools();

	/// <summary>The armed tool; null when none is.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(IsPlacingAlert))]
	public partial ChartTool? ActiveTool { get; set; }

	/// <summary>The alert tool is armed: the chart previews the line and the next click places it.</summary>
	public bool IsPlacingAlert => ActiveTool == ChartTool.Alert;

	/// <summary>Menu and the bar's ✕: shows or hides the toolbox bar.</summary>
	[RelayCommand]
	public void ToggleToolbox() => ShowToolbox = !ShowToolbox;

	/// <summary>A toolbox button: arms its tool, or disarms it when it is the armed one.</summary>
	[RelayCommand]
	public void SelectTool(ChartToolItem? item)
	{
		if (item == null)
			return;
		ActiveTool = ActiveTool == item.Tool || !CanUseTool(item.Tool) ? null : item.Tool;
	}

	/// <summary>
	/// Esc: disarms the armed tool (the cursor again for a drawing tool), else drops the selected
	/// drawing. False when there was nothing to undo.
	/// </summary>
	public bool CancelTool()
	{
		if (ActiveTool == null)
			return CancelDrawingTool();
		ActiveTool = null;
		return true;
	}

	/// <summary>
	/// The price chart was clicked with the alert tool armed: adds an alert at the price (a
	/// refusal shows on the chart) and disarms the tool.
	/// </summary>
	public void PlaceAlert(double? price)
	{
		if (!IsPlacingAlert)
			return;
		ActiveTool = null;
		AddAlert(price);
	}

	partial void OnActiveToolChanged(ChartTool? value)
	{
		foreach (var item in Tools)
			item.IsActive = item.Tool == value;
		// One armed tool at a time: an alert tool puts the drawing tool down
		if (value != null)
			ActiveDrawingKind = null;
	}

	// A hidden bar leaves no tool armed
	partial void OnShowToolboxChanged(bool value)
	{
		if (!value)
			ActiveTool = null;
	}

	// The bars came or went: a tool that can no longer be used is disarmed
	private void RefreshTools()
	{
		foreach (var item in Tools)
			item.IsEnabled = CanUseTool(item.Tool);
		if (ActiveTool is { } tool && !CanUseTool(tool))
			ActiveTool = null;
		RefreshDrawingTools();
	}

	private bool CanUseTool(ChartTool tool) =>
		tool switch
		{
			ChartTool.Alert => CanAddAlert,
			_ => false,
		};

	private List<ChartToolItem> CreateTools()
	{
		List<ChartToolItem> tools =
		[
			new(
				this,
				ChartTool.Alert,
				"Alert",
				"Add a price alert: click the chart at its price (Esc cancels)",
				AlertToolIcon
			),
		];
		foreach (var item in tools)
			item.IsEnabled = CanUseTool(item.Tool);
		return tools;
	}
}
