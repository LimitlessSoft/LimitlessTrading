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

	// IB Gateway started by the app (see GatewayManager) is the only supported endpoint
	public const int GatewayLivePort = 4001;
	public const int GatewayPaperPort = 4002;

	public const int DefaultPort = GatewayLivePort;
	public static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(15);

	public const int DefaultHeartbeatSeconds = 5;
	public const int HeartbeatSecondsMin = 2;
	public const int HeartbeatSecondsMax = 60;
	public static readonly TimeSpan HeartbeatTimeout = TimeSpan.FromSeconds(10);

	/// <summary>Delay before reconnect attempt n (0-based); the last value repeats.</summary>
	public static readonly IReadOnlyList<TimeSpan> ReconnectDelays =
	[
		TimeSpan.FromSeconds(2),
		TimeSpan.FromSeconds(5),
		TimeSpan.FromSeconds(10),
		TimeSpan.FromSeconds(30),
		TimeSpan.FromSeconds(60),
	];
}
