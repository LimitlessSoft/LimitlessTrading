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

	/// <summary>Zone times are shown in; null = the system's local zone.</summary>
	public string? DisplayTimeZoneId { get; set; }

	/// <summary>Intraday charts include pre- and post-market bars.</summary>
	public bool ChartOutsideRegularHours { get; set; } = true;
}
