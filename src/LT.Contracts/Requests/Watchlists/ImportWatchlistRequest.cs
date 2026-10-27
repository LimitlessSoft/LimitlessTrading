using LT.Contracts.Dtos.Watchlists;

namespace LT.Contracts.Requests.Watchlists;

/// <summary>A watchlist read from an exported file, checked before it is added.</summary>
public class ImportWatchlistRequest
{
	public string? Format { get; set; }
	public string? Name { get; set; }

	/// <summary>The custom columns' names, in their order.</summary>
	public IReadOnlyList<string> Columns { get; set; } = [];

	public IReadOnlyList<WatchlistImportItemDto> Items { get; set; } = [];
}
