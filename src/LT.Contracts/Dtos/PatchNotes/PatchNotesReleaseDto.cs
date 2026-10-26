namespace LT.Contracts.Dtos.PatchNotes;

/// <summary>One released version and what changed in it.</summary>
public class PatchNotesReleaseDto
{
	/// <summary>e.g. "0.10.0" (the tag without its "v").</summary>
	public string Version { get; set; } = string.Empty;
	public string PageUrl { get; set; } = string.Empty;
	public DateTimeOffset? PublishedAt { get; set; }

	/// <summary>The version this app is running.</summary>
	public bool IsCurrent { get; set; }

	/// <summary>The changes, in the release's order; empty when it lists none.</summary>
	public List<PatchNoteEntryDto> Entries { get; set; } = [];
}

public class PatchNoteEntryDto
{
	/// <summary>The change, without the "by @author in &lt;link&gt;" GitHub adds.</summary>
	public string Text { get; set; } = string.Empty;

	/// <summary>The pull request the change came in, when the line names one.</summary>
	public int? PullRequestNumber { get; set; }
	public string? PullRequestUrl { get; set; }
}
