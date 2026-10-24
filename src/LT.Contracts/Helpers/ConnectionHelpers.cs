using LT.Contracts.Constants;

namespace LT.Contracts.Helpers;

public static class ConnectionHelpers
{
	/// <summary>Human readable name of the IB Gateway ports.</summary>
	public static string DescribePort(int port) =>
		port switch
		{
			ConnectionConstants.GatewayLivePort => "Gateway live",
			ConnectionConstants.GatewayPaperPort => "Gateway paper",
			_ => "custom port",
		};

	public static bool IsLivePort(int port) => port == ConnectionConstants.GatewayLivePort;

	public static string FormatEndpoint(string host, int port, int clientId) =>
		$"{host}:{port} ({DescribePort(port)}, client {clientId})";

	/// <summary>Splits TWS's comma separated managedAccounts value.</summary>
	public static IReadOnlyList<string> ParseAccounts(string? managedAccounts) =>
		string.IsNullOrWhiteSpace(managedAccounts)
			? []
			: managedAccounts.Split(
				',',
				StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
			);

	/// <summary>Backoff before reconnect attempt <paramref name="attempt"/> (0-based).</summary>
	public static TimeSpan GetReconnectDelay(int attempt) =>
		ConnectionConstants.ReconnectDelays[
			Math.Clamp(attempt, 0, ConnectionConstants.ReconnectDelays.Count - 1)
		];
}
