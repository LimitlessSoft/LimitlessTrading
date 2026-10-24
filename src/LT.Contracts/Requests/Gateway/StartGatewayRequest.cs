using LT.Contracts.Enums;

namespace LT.Contracts.Requests.Gateway;

public class StartGatewayRequest
{
	public string Username { get; set; } = string.Empty;
	public string Password { get; set; } = string.Empty;
	public TradingMode TradingMode { get; set; }

	/// <summary>IBC install folder (contains scripts/ibcstart.sh, scripts/StartIBC.bat).</summary>
	public string IbcPath { get; set; } = string.Empty;

	/// <summary>Root of the Gateway install, IBC's tws-path (e.g. ~/Jts, C:\Jts).</summary>
	public string GatewayPath { get; set; } = string.Empty;

	public string GatewayVersion { get; set; } = string.Empty;

	/// <summary>Linux only: run Gateway under xvfb-run so no window appears.</summary>
	public bool UseVirtualDisplay { get; set; }
}
