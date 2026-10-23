namespace LT.Contracts.Requests.Connection;

public class ConnectRequest
{
	public string Host { get; set; } = string.Empty;
	public int Port { get; set; }
	public int ClientId { get; set; }
}
