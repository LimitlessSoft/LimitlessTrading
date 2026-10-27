namespace LT.Contracts.Dtos.Watchlists;

/// <summary>
/// The watchlist an import created, and what IB couldn't find: symbols of a symbol list, or
/// file items without a conId that weren't found or stayed ambiguous.
/// </summary>
public class WatchlistImportResultDto
{
	public WatchlistDto Watchlist { get; set; } = new();
	public IReadOnlyList<string> NotFound { get; set; } = [];
}
