using LT.Contracts.Enums;

namespace LT.Contracts.Dtos.Updates;

public class UpdateStatusDto
{
	public UpdateState State { get; set; }
	public string CurrentVersion { get; set; } = string.Empty;

	/// <summary>Version of the newer release (Available onwards), e.g. "0.2.0".</summary>
	public string? LatestVersion { get; set; }

	/// <summary>The release's page ("What's new").</summary>
	public string? ReleasePageUrl { get; set; }

	/// <summary>Why this copy can't install the update itself (null: it can).</summary>
	public string? CannotInstallReason { get; set; }

	/// <summary>0–1 while downloading.</summary>
	public double? Progress { get; set; }

	public string? Error { get; set; }
}
