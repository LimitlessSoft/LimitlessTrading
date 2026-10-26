namespace LT.Contracts.Entities;

/// <summary>
/// The patch notes last fetched from GitHub (patch-notes.json), so they open offline, and the
/// last version the user ran, so the first start on a new one can show what changed.
/// </summary>
public class PatchNotesStateEntity
{
	/// <summary>The releases as GitHub sent them; parsed on read, so parser fixes apply to them.</summary>
	public List<PatchNotesReleaseEntity> Releases { get; set; } = [];
	public DateTimeOffset? FetchedAt { get; set; }

	/// <summary>Normalized version of the last start (e.g. "0.10.0"); null before the first.</summary>
	public string? LastSeenVersion { get; set; }
}

public class PatchNotesReleaseEntity
{
	public string Tag { get; set; } = string.Empty;
	public string PageUrl { get; set; } = string.Empty;
	public DateTimeOffset? PublishedAt { get; set; }

	/// <summary>The release notes, Markdown.</summary>
	public string Body { get; set; } = string.Empty;
}
