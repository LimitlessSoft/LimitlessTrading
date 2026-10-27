namespace LT.Contracts.Dtos.Watchlists;

/// <summary>The watchlist an import created, and the symbols of a symbol list IB didn't know.</summary>
public class WatchlistImportResultDto
{
	public WatchlistDto Watchlist { get; set; } = new();
	public IReadOnlyList<string> NotFound { get; set; } = [];
}
