namespace LT.Contracts.Dtos.Gateway;

/// <summary>Everything needed to start IBC as a child process.</summary>
public class IbcLaunchDto
{
	public string FileName { get; set; } = string.Empty;
	public IReadOnlyList<string> Arguments { get; set; } = [];

	/// <summary>Written owner-only before start and deleted once login is done — holds the password.</summary>
	public string ConfigFilePath { get; set; } = string.Empty;
	public string ConfigContent { get; set; } = string.Empty;
}
