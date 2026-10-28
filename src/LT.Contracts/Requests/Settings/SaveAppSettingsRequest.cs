using LT.Contracts.Constants;
using LT.Contracts.Enums;

namespace LT.Contracts.Requests.Settings;

public class SaveAppSettingsRequest
{
	public MarketDataType MarketDataType { get; set; }
	public bool AutoReconnect { get; set; }
	public int HeartbeatSeconds { get; set; }
	public bool StopGatewayOnExit { get; set; }
	public bool CheckForUpdates { get; set; }
	public LayoutStyle LayoutStyle { get; set; }
	public bool CacheEnabled { get; set; } = true;
	public bool CacheChartBars { get; set; } = true;
	public bool CacheContracts { get; set; } = true;
	public bool CacheOptionChains { get; set; } = true;
	public bool CacheExecutions { get; set; } = true;
	public bool CacheSnapshots { get; set; } = true;
	public int CacheMaxSizeMb { get; set; } = CacheConstants.DefaultMaxSizeMb;
	public ChartLiveUpdateMode ChartLiveUpdateMode { get; set; } = ChartLiveUpdateMode.RealtimeBars;

	/// <summary>Used only with the throttled <see cref="ChartLiveUpdateMode"/>s.</summary>
	public int ChartRedrawIntervalMs { get; set; } = ChartConstants.DefaultRedrawIntervalMs;

	/// <summary>
	/// Footprint bars: a cell is an imbalance once its volume is this percent of the diagonal
	/// opposite cell (<see cref="Constants.FootprintConstants"/>).
	/// </summary>
	public int ChartFootprintImbalancePercent { get; set; } =
		FootprintConstants.DefaultImbalancePercent;

	/// <summary>Zone times are shown in; null = the system's local zone.</summary>
	public string? DisplayTimeZoneId { get; set; }

	/// <summary>Play a short sound when an order fills (each new execution).</summary>
	public bool PlayFillSound { get; set; } = true;

	/// <summary>Calendar widget: show nasdaq.com's earnings, dividends, splits and economic releases (no setup).</summary>
	public bool CalendarNasdaq { get; set; } = true;

	/// <summary>
	/// Calendar widget: show IB's Wall Street Horizon corporate events. Off by default: it needs a
	/// paid IB subscription most accounts don't have (IB then refuses with 10276).
	/// </summary>
	public bool CalendarWallStreetHorizon { get; set; }
}
