namespace LT.Contracts.Entities;

/// <summary>A named list of contracts; the watchlist widget shows the active one.</summary>
public class WatchlistEntity
{
	public string Id { get; set; } = Guid.NewGuid().ToString("N");
	public string Name { get; set; } = string.Empty;
	public List<ContractEntity> Items { get; set; } = [];
}
