using LT.Contracts.Enums;

namespace LT.Contracts.Entities;

/// <summary>An open widget instance and where it lives; closed widgets have no entry.</summary>
public class WidgetPlacementEntity
{
	/// <summary>Instance id; widgets that allow several instances (charts) have one each.</summary>
	public string Id { get; set; } = string.Empty;
	public WorkspaceTool Tool { get; set; }
	public WidgetDock Dock { get; set; }
	public double? FloatX { get; set; }
	public double? FloatY { get; set; }
	public double? FloatWidth { get; set; }
	public double? FloatHeight { get; set; }

	// Linkable widgets (chart, option chain, watchlist, positions)
	public LinkGroup LinkGroup { get; set; } = LinkGroup.Red;

	// Side widgets: pinned stays expanded; unpinned collapses when you click elsewhere
	public bool IsPinned { get; set; } = true;

	/// <summary>Side widgets: their own panel width (null: the side's last width).</summary>
	public double? SideWidth { get; set; }

	/// <summary>Bottom widgets: their own panel height (null: the bottom's last height).</summary>
	public double? BottomHeight { get; set; }

	// Chart instances
	public ChartTimeframe? ChartTimeframe { get; set; }
	public bool ChartLinked { get; set; } = true;
	public ContractEntity? ChartContract { get; set; }
}
