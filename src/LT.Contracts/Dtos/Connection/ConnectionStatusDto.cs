using LT.Contracts.Enums;

namespace LT.Contracts.Dtos.Connection;

public class ConnectionStatusDto
{
	public ConnectionState State { get; set; }
	public string? Endpoint { get; set; }
	public IReadOnlyList<string> Accounts { get; set; } = [];
	public DateTimeOffset? ServerTime { get; set; }
	public string? Error { get; set; }
}
