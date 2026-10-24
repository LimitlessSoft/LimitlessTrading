namespace LT.Contracts.Constants;

/// <summary>IB Gateway launched through IBC (https://github.com/IbcAlpha/IBC).</summary>
public static class GatewayConstants
{
	public const int UsernameMaxLength = 64;
	public const int PasswordMaxLength = 128;
	public const int PathMaxLength = 1024;

	/// <summary>Gateway major version as IBC expects it, e.g. 10.37 → "1037".</summary>
	public const string VersionPattern = @"^\d{3,4}$";
	public const string DefaultGatewayVersion = "1037";

	/// <summary>How deep the locator searches below each search root for scripts/ibcstart.sh.</summary>
	public const int IbcSearchDepth = 4;

	/// <summary>GitHub API for IBC's latest release; assets are IBCLinux-/IBCMacos-/IBCWin-x.y.z.zip.</summary>
	public const string IbcLatestReleaseUrl =
		"https://api.github.com/repos/IbcAlpha/IBC/releases/latest";
	public const string IbcInstallDirectoryName = "ibc";

	/// <summary>Releases are ~300 KB; anything far bigger isn't IBC.</summary>
	public const long IbcMaxDownloadBytes = 20 * 1024 * 1024;
	public static readonly TimeSpan IbcDownloadTimeout = TimeSpan.FromMinutes(2);

	/// <summary>IBC's command server; we only use it to send STOP.</summary>
	public const int IbcCommandServerPort = 7462;
	public const string IbcConfigFileName = "ibc-config.ini";

	/// <summary>Long enough for the user to approve the IB Key 2FA push on their phone.</summary>
	public static readonly TimeSpan StartTimeout = TimeSpan.FromMinutes(5);
	public static readonly TimeSpan StartPollInterval = TimeSpan.FromSeconds(1);
	public static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(20);
	public static readonly TimeSpan PortProbeTimeout = TimeSpan.FromMilliseconds(500);
}
