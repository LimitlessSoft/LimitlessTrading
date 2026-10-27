namespace LT.Contracts.Entities;

/// <summary>An exported watchlist: its items in saved order, custom columns and their values.</summary>
public class WatchlistFileEntity
{
	/// <summary><c>WatchlistConstants.FileFormat</c>; anything else is not a watchlist file.</summary>
	public string? Format { get; set; }

	public int Version { get; set; }
	public string? Name { get; set; }

	/// <summary>The custom columns' names, in their order.</summary>
	public List<string>? Columns { get; set; }

	public List<WatchlistFileItemEntity>? Items { get; set; }
}
