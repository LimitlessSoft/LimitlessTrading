namespace LT.Contracts.Entities;

/// <summary>One item of an exported watchlist.</summary>
public class WatchlistFileItemEntity
{
	public ContractEntity? Contract { get; set; }

	/// <summary>Its place among the pinned items (0 = pinned first); null when not pinned.</summary>
	public int? Pin { get; set; }

	/// <summary>Custom column values by column name; empty cells are left out.</summary>
	public Dictionary<string, string>? Values { get; set; }
}
