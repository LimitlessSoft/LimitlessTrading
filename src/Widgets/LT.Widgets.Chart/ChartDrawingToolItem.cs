using System.Windows.Input;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using LT.Contracts.Enums;

namespace LT.App.Widgets.Chart;

/// <summary>
/// A button of the drawing palette (drawing mode, at the plot's left): a drawing tool (armed by
/// it, <see cref="Kind"/>; the cursor has none) or an action on the drawings (delete, lock, hide).
/// </summary>
public partial class ChartDrawingToolItem(
	string label,
	string toolTip,
	string icon,
	ICommand command,
	ChartDrawingKind? kind = null
) : ObservableObject
{
	public string Label => label;

	/// <summary>Stroked (not filled) path on a 24 × 24 grid.</summary>
	public Geometry Icon => field ??= Geometry.Parse(icon);

	/// <summary>Runs it; the item is the parameter.</summary>
	public ICommand Command => command;

	/// <summary>The drawing it draws; null for the cursor and the actions.</summary>
	public ChartDrawingKind? Kind => kind;

	/// <summary>The cursor or a drawing tool (not an action).</summary>
	public bool IsTool { get; init; }

	[ObservableProperty]
	public partial string ToolTip { get; set; } = toolTip;

	/// <summary>The armed tool, or an action that is on (lock, hide).</summary>
	[ObservableProperty]
	public partial bool IsActive { get; set; }

	[ObservableProperty]
	public partial bool IsEnabled { get; set; } = true;
}

/// <summary>A group of the drawing palette; groups are split by a line.</summary>
public class ChartDrawingToolGroup(IReadOnlyList<ChartDrawingToolItem> items)
{
	public IReadOnlyList<ChartDrawingToolItem> Items => items;
}
