using LT.Contracts.Dtos.Contracts;

namespace LT.Contracts.Dtos.Watchlists;

/// <summary>One item of an imported watchlist file.</summary>
public class WatchlistImportItemDto
{
	/// <summary>Null when the file's item has none (refused by validation).</summary>
	public ContractDto? Contract { get; set; }

	/// <summary>Its place among the pinned items; null when not pinned.</summary>
	public int? Pin { get; set; }

	/// <summary>Custom column values by column name (case-insensitive).</summary>
	public IReadOnlyDictionary<string, string> Values { get; set; } =
		new Dictionary<string, string>();
}
