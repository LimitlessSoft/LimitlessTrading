using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Enums;

namespace LT.Contracts.Dtos.Settings;

public class WidgetPlacementDto
{
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

	// Chart instances
	public ChartTimeframe? ChartTimeframe { get; set; }
	public bool ChartLinked { get; set; } = true;
	public ContractDto? ChartContract { get; set; }
}
