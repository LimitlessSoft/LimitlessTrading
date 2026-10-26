namespace LT.Contracts.Constants;

/// <summary>IB Gateway started from its own launcher; the user logs in in its window.</summary>
public static class GatewayConstants
{
	public const int PathMaxLength = 1024;

	/// <summary>Gateway major version as in its install folder, e.g. 10.37 → "1037".</summary>
	public const string VersionPattern = @"^\d{3,4}$";
	public const string DefaultGatewayVersion = "1037";

	/// <summary>Long enough to type the credentials and approve the IB Key 2FA push.</summary>
	public static readonly TimeSpan StartTimeout = TimeSpan.FromMinutes(10);
	public static readonly TimeSpan StartPollInterval = TimeSpan.FromSeconds(1);
	public static readonly TimeSpan PortProbeTimeout = TimeSpan.FromMilliseconds(500);
}
