using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace LT.App.Widgets.Chart;

/// <summary>A button of the chart's toolbox bar: arms its tool, shown as active while armed.</summary>
public partial class ChartToolItem(
	ChartViewModel owner,
	ChartTool tool,
	string label,
	string toolTip,
	string icon
) : ObservableObject
{
	/// <summary>The chart: the button runs its command.</summary>
	public ChartViewModel Owner => owner;
	public ChartTool Tool => tool;
	public string Label => label;
	public string ToolTip => toolTip;

	/// <summary>Material Design path on a 24 × 24 grid.</summary>
	public Geometry Icon => field ??= Geometry.Parse(icon);

	/// <summary>Armed: the next click on the chart uses it.</summary>
	[ObservableProperty]
	public partial bool IsActive { get; set; }

	/// <summary>The tool can be used now (the alert tool needs a contract with bars).</summary>
	[ObservableProperty]
	public partial bool IsEnabled { get; set; }
}
