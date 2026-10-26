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

	/// <summary>Floating window state (null: normal) and screen (null: older layouts).</summary>
	public WindowPlacementState? FloatState { get; set; }
	public WindowScreenEntity? FloatScreen { get; set; }

	// Linkable widgets (chart, option chain, watchlist, positions, orders, executions, news,
	// order ticket)
	public LinkGroup LinkGroup { get; set; } = LinkGroup.Red;

	// Side widgets: pinned stays expanded; unpinned collapses when you click elsewhere
	public bool IsPinned { get; set; } = true;

	/// <summary>Side widgets: their own panel width (null: the side's last width).</summary>
	public double? SideWidth { get; set; }

	/// <summary>Bottom widgets: their own panel height (null: the bottom's last height).</summary>
	public double? BottomHeight { get; set; }

	// Chart instances
	public ChartTimeframe? ChartTimeframe { get; set; }

	/// <summary>Chart instances: the span of bars shown (null: older layouts, Auto).</summary>
	public ChartPeriod? ChartPeriod { get; set; }
	public bool ChartLinked { get; set; } = true;
	public ContractEntity? ChartContract { get; set; }

	/// <summary>Chart instances: empty bars after the latest one (null: none).</summary>
	public double? ChartRightMargin { get; set; }

	/// <summary>Chart instances: their indicators in legend order (null: none; older layouts).</summary>
	public List<IndicatorInstanceEntity>? ChartIndicators { get; set; }

	/// <summary>Chart instances: the template last applied to or saved from it (null: none).</summary>
	public string? ChartTemplateId { get; set; }

	/// <summary>
	/// Chart instances: follows <see cref="ChartTemplateId"/> — its changes are saved to the
	/// template and the template's changes applied to it.
	/// </summary>
	public bool ChartFollowTemplate { get; set; }

	// News instances: what they show and which providers (keys "ibkr:BRFG"; null: all of them)
	public NewsMode? NewsMode { get; set; }
	public List<string>? NewsProviders { get; set; }

	/// <summary>Option chain instances: contracts per one-click order (null: the default).</summary>
	public int? OptionQuickOrderQuantity { get; set; }

	/// <summary>Account performance: deposits, withdrawals and transfers marked (null: off).</summary>
	public bool? PerformanceShowCashMovements { get; set; }
}
