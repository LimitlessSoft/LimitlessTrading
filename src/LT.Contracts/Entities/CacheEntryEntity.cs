namespace LT.Contracts.Entities;

/// <summary>One cache file: the value and when it was fetched from IB.</summary>
public class CacheEntryEntity<T>
{
	public string Key { get; set; } = string.Empty;
	public DateTimeOffset FetchedAt { get; set; }
	public T? Value { get; set; }
}
