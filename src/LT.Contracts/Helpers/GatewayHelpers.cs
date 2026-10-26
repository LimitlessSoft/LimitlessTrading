using LT.Contracts.Constants;
using LT.Contracts.Enums;

namespace LT.Contracts.Helpers;

/// <summary>Gateway install defaults and its API ports.</summary>
public static class GatewayHelpers
{
	/// <summary>Gateway's own install defaults.</summary>
	public static string GetDefaultGatewayPath() =>
		OperatingSystem.IsWindows() ? @"C:\Jts"
		: OperatingSystem.IsMacOS() ? Path.Combine(GetHome(), "Applications")
		: Path.Combine(GetHome(), "Jts");

	public static int GetApiPort(TradingMode mode) =>
		mode == TradingMode.Live
			? ConnectionConstants.GatewayLivePort
			: ConnectionConstants.GatewayPaperPort;

	/// <summary>
	/// Both API ports, <paramref name="preferred"/>'s first: the user picks live or paper in
	/// Gateway's login window, so the app watches both and uses whichever opens.
	/// </summary>
	public static IReadOnlyList<(TradingMode Mode, int Port)> GetApiPorts(TradingMode preferred)
	{
		var other = preferred == TradingMode.Live ? TradingMode.Paper : TradingMode.Live;
		return [(preferred, GetApiPort(preferred)), (other, GetApiPort(other))];
	}

	private static string GetHome() =>
		Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
}
