namespace LT.Contracts.Dtos.Watchlists;

/// <summary>A custom column of a watchlist with its non-empty cells.</summary>
public class WatchlistColumnDto
{
	public string Id { get; set; } = string.Empty;
	public string Name { get; set; } = string.Empty;
	public IReadOnlyList<WatchlistCellDto> Cells { get; set; } = [];
}
