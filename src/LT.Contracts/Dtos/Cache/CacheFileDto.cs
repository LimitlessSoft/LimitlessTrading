namespace LT.Contracts.Dtos.Cache;

/// <summary>One file of the cache on disk, as seen by the size limit.</summary>
public class CacheFileDto
{
	public string Path { get; set; } = string.Empty;
	public long SizeBytes { get; set; }
	public DateTimeOffset WrittenAt { get; set; }
}
