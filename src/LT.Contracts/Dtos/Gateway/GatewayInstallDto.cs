namespace LT.Contracts.Dtos.Gateway;

/// <summary>What was found on disk; null / empty means not found.</summary>
public class GatewayInstallDto
{
	public string? GatewayPath { get; set; }

	/// <summary>Installed Gateway major versions, newest first (e.g. "1037").</summary>
	public IReadOnlyList<string> GatewayVersions { get; set; } = [];
}
