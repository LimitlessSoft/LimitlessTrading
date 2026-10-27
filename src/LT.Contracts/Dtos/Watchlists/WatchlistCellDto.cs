namespace LT.Contracts.Dtos.Watchlists;

/// <summary>A custom column's value for one item, by its listing (conId and exchange).</summary>
public class WatchlistCellDto
{
	public int ConId { get; set; }
	public string Exchange { get; set; } = string.Empty;
	public string Value { get; set; } = string.Empty;
}
