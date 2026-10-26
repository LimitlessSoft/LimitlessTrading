namespace LT.Contracts.Dtos.Updates;

/// <summary>A published release of the app (GitHub Releases).</summary>
public class AppReleaseDto
{
	/// <summary>The tag, e.g. "v0.2.0".</summary>
	public string Tag { get; set; } = string.Empty;
	public string PageUrl { get; set; } = string.Empty;
	public DateTimeOffset? PublishedAt { get; set; }

	/// <summary>The release notes, Markdown (GitHub's generated "What's Changed" list).</summary>
	public string Body { get; set; } = string.Empty;
	public List<AppReleaseAssetDto> Assets { get; set; } = [];
}

public class AppReleaseAssetDto
{
	public string Name { get; set; } = string.Empty;
	public string DownloadUrl { get; set; } = string.Empty;
	public long Size { get; set; }

	/// <summary>GitHub's checksum of the file, e.g. "sha256:ab12…"; null on older releases.</summary>
	public string? Digest { get; set; }
}
