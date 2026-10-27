namespace LT.Contracts.Entities;

/// <summary>
/// A custom column of a watchlist: a name and the text the user typed per item. Watchlists saved
/// before custom columns have none.
/// </summary>
public class WatchlistColumnEntity
{
	public string Id { get; set; } = Guid.NewGuid().ToString("N");
	public string Name { get; set; } = string.Empty;

	/// <summary>The non-empty cells, by listing (conId and exchange), in no particular order.</summary>
	public List<WatchlistCellEntity> Cells { get; set; } = [];
}
