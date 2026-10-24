using LT.Contracts.Enums;

namespace LT.Contracts.Dtos.Gateway;

public class GatewayStatusDto
{
	public GatewayState State { get; set; }
	public TradingMode? TradingMode { get; set; }
	public int? ApiPort { get; set; }
	public string? Error { get; set; }
}
