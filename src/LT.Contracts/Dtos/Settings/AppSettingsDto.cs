using LT.Contracts.Enums;

namespace LT.Contracts.Dtos.Settings;

public class AppSettingsDto
{
	public MarketDataType MarketDataType { get; set; }
	public bool AutoReconnect { get; set; }
	public int HeartbeatSeconds { get; set; }
}
