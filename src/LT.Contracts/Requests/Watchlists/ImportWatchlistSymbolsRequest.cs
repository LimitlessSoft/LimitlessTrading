namespace LT.Contracts.Requests.Watchlists;

/// <summary>The symbols of a plain list (one per line or comma-separated), checked before any is looked up.</summary>
public class ImportWatchlistSymbolsRequest
{
	public IReadOnlyList<string> Symbols { get; set; } = [];
}
