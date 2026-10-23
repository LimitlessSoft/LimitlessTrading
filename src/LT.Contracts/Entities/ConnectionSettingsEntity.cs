using LT.Contracts.Constants;

namespace LT.Contracts.Entities;

public class ConnectionSettingsEntity
{
	public string Host { get; set; } = ConnectionConstants.DefaultHost;
	public int Port { get; set; } = ConnectionConstants.DefaultPort;
	public int ClientId { get; set; } = ConnectionConstants.DefaultClientId;
}
