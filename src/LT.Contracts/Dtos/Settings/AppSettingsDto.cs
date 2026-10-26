using LT.Contracts.Enums;

namespace LT.Contracts.Dtos.Settings;

public class AppSettingsDto
{
	public MarketDataType MarketDataType { get; set; }
	public bool AutoReconnect { get; set; }
	public int HeartbeatSeconds { get; set; }
	public bool StopGatewayOnExit { get; set; }
	public bool CheckForUpdates { get; set; }
	public LayoutStyle LayoutStyle { get; set; }
	public bool CacheEnabled { get; set; }
	public bool CacheChartBars { get; set; }
	public bool CacheContracts { get; set; }
	public bool CacheOptionChains { get; set; }
	public bool CacheExecutions { get; set; }
	public bool CacheSnapshots { get; set; }
	public int CacheMaxSizeMb { get; set; }
}
