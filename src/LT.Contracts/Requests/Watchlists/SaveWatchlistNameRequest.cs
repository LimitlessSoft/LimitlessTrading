namespace LT.Contracts.Requests.Watchlists;

/// <summary>Rename a watchlist; <see cref="Id"/> null creates a new one with this name.</summary>
public class SaveWatchlistNameRequest
{
	public string? Id { get; set; }
	public string Name { get; set; } = string.Empty;
}
