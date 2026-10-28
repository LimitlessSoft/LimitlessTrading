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

	/// <summary>
	/// Chart instances: intraday bars include pre- and post-market trading (IB useRTH=false);
	/// null (older layouts) is on, the default.
	/// </summary>
	public bool? ChartOutsideRegularHours { get; set; }
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

	/// <summary>Chart instances: the toolbox bar above the plot is shown (null: older layouts, shown).</summary>
	public bool? ChartShowToolbox { get; set; }

	/// <summary>Chart instances: drawing mode is on (the tool palette at the plot's left; null: off).</summary>
	public bool? ChartDrawingMode { get; set; }

	/// <summary>Chart instances: its drawings can't be selected, moved or deleted one by one (null: no).</summary>
	public bool? ChartDrawingsLocked { get; set; }

	/// <summary>Chart instances: its drawings are hidden (null: shown).</summary>
	public bool? ChartDrawingsHidden { get; set; }

	// News instances: what they show and which providers (keys "ibkr:BRFG"; null: all of them)
	public NewsMode? NewsMode { get; set; }
	public List<string>? NewsProviders { get; set; }

	/// <summary>Option chain instances: contracts per one-click order (null: the default).</summary>
	public int? OptionQuickOrderQuantity { get; set; }

	/// <summary>Account performance: deposits, withdrawals and transfers marked (null: off).</summary>
	public bool? PerformanceShowCashMovements { get; set; }

	/// <summary>Watchlist instances: the watchlist shown (null or deleted: the active one).</summary>
	public string? WatchlistId { get; set; }

	/// <summary>Watchlist instances: built-in columns hidden (null: none; older layouts).</summary>
	public List<WatchlistSortColumn>? WatchlistHiddenColumns { get; set; }

	/// <summary>Watchlist instances: custom columns hidden, by column id (null: none).</summary>
	public List<string>? WatchlistHiddenCustomColumns { get; set; }

	/// <summary>
	/// Watchlist instances: the column the unpinned rows are sorted by, hidden or not (null:
	/// older layouts, the saved order); a custom one by <see cref="WatchlistSortCustomColumnId"/>.
	/// </summary>
	public WatchlistSortColumn? WatchlistSortBy { get; set; }
	public string? WatchlistSortCustomColumnId { get; set; }

	/// <summary>Watchlist instances: the sort runs high to low (null: ascending).</summary>
	public bool? WatchlistSortDescending { get; set; }

	/// <summary>Notes instances: the note shown (null: a new, empty one).</summary>
	public string? NoteId { get; set; }

	/// <summary>Calendar instances: the days and events shown (null: this week, everything).</summary>
	public CalendarRange? CalendarRange { get; set; }
	public CalendarEventFilter? CalendarFilter { get; set; }
}
