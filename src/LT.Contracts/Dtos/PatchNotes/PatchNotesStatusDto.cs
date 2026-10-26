using LT.Contracts.Enums;

namespace LT.Contracts.Dtos.PatchNotes;

public class PatchNotesStatusDto
{
	public PatchNotesState State { get; set; }

	/// <summary>The running version, e.g. "0.10.0".</summary>
	public string CurrentVersion { get; set; } = string.Empty;

	/// <summary>Newest first; the cached list until a refresh succeeds.</summary>
	public List<PatchNotesReleaseDto> Releases { get; set; } = [];

	/// <summary>When <see cref="Releases"/> was fetched from GitHub; null when never.</summary>
	public DateTimeOffset? FetchedAt { get; set; }

	/// <summary>Why the last refresh failed (Failed only).</summary>
	public string? Error { get; set; }
}
