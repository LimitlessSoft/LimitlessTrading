using LT.Contracts.Constants;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;

namespace LT.Contracts.Entities;

/// <summary>
/// The Gateway install to start and the mode last logged in to. Credentials are never stored:
/// the user types them in Gateway's own window. Settings files from the IBC era also carry
/// Username / IbcPath / UseVirtualDisplay; they are ignored when read.
/// </summary>
public class GatewaySettingsEntity
{
	/// <summary>Mode of the last Gateway connected to; its port is tried first when attaching.</summary>
	public TradingMode TradingMode { get; set; } = TradingMode.Live;
	public string GatewayPath { get; set; } = GatewayHelpers.GetDefaultGatewayPath();
	public string GatewayVersion { get; set; } = GatewayConstants.DefaultGatewayVersion;
}
