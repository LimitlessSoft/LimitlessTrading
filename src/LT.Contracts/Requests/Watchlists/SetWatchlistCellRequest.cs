using LT.Contracts.Dtos.Contracts;

namespace LT.Contracts.Requests.Watchlists;

/// <summary>A custom column's value for one item; blank clears it.</summary>
public class SetWatchlistCellRequest
{
	public string WatchlistId { get; set; } = string.Empty;
	public string ColumnId { get; set; } = string.Empty;
	public ContractDto Contract { get; set; } = new();
	public string? Value { get; set; }
}
