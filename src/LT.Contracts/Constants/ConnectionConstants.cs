namespace LT.Contracts.Constants;

public static class ConnectionConstants
{
	public const string DefaultHost = "127.0.0.1";
	public const int DefaultClientId = 1;
	public const int HostMaxLength = 253;
	public const int PortMin = 1;
	public const int PortMax = 65535;
	public const int ClientIdMin = 0;
	public const int ClientIdMax = 32767;

	public const int TwsLivePort = 7496;
	public const int TwsPaperPort = 7497;
	public const int GatewayLivePort = 4001;
	public const int GatewayPaperPort = 4002;

	public const int DefaultPort = TwsPaperPort;
	public static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(15);
}
