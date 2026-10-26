namespace LT.Contracts.Dtos.Cache;

/// <summary>A value from IB or from the local cache, with when it was fetched.</summary>
public class CachedDto<T>
{
	public required T Value { get; set; }
	public DateTimeOffset FetchedAt { get; set; }

	/// <summary>True when IB could not be reached and this is the last cached copy.</summary>
	public bool IsStale { get; set; }
	public string? Error { get; set; }
}
