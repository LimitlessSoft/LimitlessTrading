namespace LT.Contracts.Entities;

/// <summary>A custom column's value for one watchlist item, by its listing (conId and exchange).</summary>
public class WatchlistCellEntity
{
	public int ConId { get; set; }
	public string Exchange { get; set; } = string.Empty;
	public string Value { get; set; } = string.Empty;
}
