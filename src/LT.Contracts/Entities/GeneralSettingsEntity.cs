using LT.Contracts.Constants;
using LT.Contracts.Enums;

namespace LT.Contracts.Entities;

public class GeneralSettingsEntity
{
	public MarketDataType MarketDataType { get; set; } = MarketDataType.Delayed;
	public bool AutoReconnect { get; set; } = true;
	public int HeartbeatSeconds { get; set; } = ConnectionConstants.DefaultHeartbeatSeconds;
	public bool StopGatewayOnExit { get; set; } = true;
}
