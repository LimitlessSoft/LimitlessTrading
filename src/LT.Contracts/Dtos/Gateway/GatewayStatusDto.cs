using LT.Contracts.Enums;

namespace LT.Contracts.Dtos.Gateway;

public class GatewayStatusDto
{
	public GatewayState State { get; set; }
	public TradingMode? TradingMode { get; set; }
	public int? ApiPort { get; set; }
	public string? Error { get; set; }

	/// <summary>
	/// Running, but not started by this app run (e.g. left over after a crash); never stopped by it.
	/// </summary>
	public bool IsExternal { get; set; }
}
