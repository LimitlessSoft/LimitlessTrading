namespace LT.Contracts.Requests.Gateway;

/// <summary>
/// Which installed Gateway to start. Credentials and live / paper are entered in Gateway's own
/// login window, never here.
/// </summary>
public class StartGatewayRequest
{
	/// <summary>Root of the Gateway install (e.g. ~/Jts, C:\Jts, ~/Applications).</summary>
	public string GatewayPath { get; set; } = string.Empty;

	/// <summary>Major version folder, e.g. "1050" for 10.50.</summary>
	public string GatewayVersion { get; set; } = string.Empty;
}
