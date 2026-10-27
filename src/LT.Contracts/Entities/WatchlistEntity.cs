namespace LT.Contracts.Entities;

/// <summary>A named list of contracts; the watchlist widget shows the active one.</summary>
public class WatchlistEntity
{
	public string Id { get; set; } = Guid.NewGuid().ToString("N");
	public string Name { get; set; } = string.Empty;
	public List<ContractEntity> Items { get; set; } = [];

	/// <summary>
	/// Items pinned to the top, in the order they were pinned. Watchlists saved before pinning
	/// have none.
	/// </summary>
	public List<WatchlistPinEntity> Pinned { get; set; } = [];

	/// <summary>The user's own columns, in their order. Watchlists saved before them have none.</summary>
	public List<WatchlistColumnEntity> Columns { get; set; } = [];
}
