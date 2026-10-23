using LT.Contracts.Constants;

namespace LT.Contracts.Helpers;

public static class ConnectionHelpers
{
	/// <summary>Human readable name of the well-known TWS / IB Gateway ports.</summary>
	public static string DescribePort(int port) =>
		port switch
		{
			ConnectionConstants.TwsLivePort => "TWS live",
			ConnectionConstants.TwsPaperPort => "TWS paper",
			ConnectionConstants.GatewayLivePort => "Gateway live",
			ConnectionConstants.GatewayPaperPort => "Gateway paper",
			_ => "custom port",
		};

	public static bool IsLivePort(int port) =>
		port is ConnectionConstants.TwsLivePort or ConnectionConstants.GatewayLivePort;

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
}
