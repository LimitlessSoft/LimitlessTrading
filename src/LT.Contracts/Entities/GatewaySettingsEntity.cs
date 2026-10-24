using LT.Contracts.Constants;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;

namespace LT.Contracts.Entities;

/// <summary>The password is never persisted — only what's needed to prefill the form.</summary>
public class GatewaySettingsEntity
{
	public string Username { get; set; } = string.Empty;
	public TradingMode TradingMode { get; set; } = TradingMode.Paper;
	public string IbcPath { get; set; } = IbcHelpers.GetDefaultIbcPath();
	public string GatewayPath { get; set; } = IbcHelpers.GetDefaultGatewayPath();
	public string GatewayVersion { get; set; } = GatewayConstants.DefaultGatewayVersion;
	public bool UseVirtualDisplay { get; set; }
}
