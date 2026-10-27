namespace LT.Contracts.Requests.Watchlists;

/// <summary>Rename a custom column; <see cref="ColumnId"/> null adds a new one with this name.</summary>
public class SaveWatchlistColumnRequest
{
	public string WatchlistId { get; set; } = string.Empty;
	public string? ColumnId { get; set; }
	public string Name { get; set; } = string.Empty;
}
