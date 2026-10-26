namespace LT.Contracts.Entities;

/// <summary>A watchlist item pinned to the top, by its listing (conId and exchange).</summary>
public class WatchlistPinEntity
{
	public int ConId { get; set; }
	public string Exchange { get; set; } = string.Empty;
}
