using LT.Contracts.Enums;

namespace LT.Contracts.Requests.Settings;

public class SaveAppSettingsRequest
{
	public MarketDataType MarketDataType { get; set; }
	public bool AutoReconnect { get; set; }
	public int HeartbeatSeconds { get; set; }
	public bool StopGatewayOnExit { get; set; }
}
