using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.Watchlists;
using LT.Contracts.Requests.Watchlists;

namespace LT.Contracts.Interfaces.IManagers;

/// <summary>
/// Named watchlists. Items and pins are read and changed on the watchlist given by id: every
/// watchlist widget shows its own. The active one is the watchlist picked last, which a new
/// watchlist widget opens on. Singleton, so every widget hears <see cref="Changed"/>.
/// </summary>
public interface IWatchlistManager
{
	/// <summary>
	/// Raised on the calling thread after a watchlist is created, renamed, deleted or imported, or
	/// its items, pins, custom columns or their values change, so other watchlist widgets showing
	/// it can follow.
	/// </summary>
	event Action? Changed;

	IReadOnlyList<WatchlistDto> GetWatchlists();
	WatchlistDto GetActive();
	void SetActive(string id);

	/// <summary>Creates a watchlist (<c>Id</c> null) or renames one; the new one becomes active.</summary>
	WatchlistDto SaveName(SaveWatchlistNameRequest request);

	/// <summary>A free default name for a new watchlist ("Watchlist 2" …).</summary>
	string GetNewName();

	/// <summary>Deletes a watchlist and its items; the last one can't be deleted.</summary>
	void Delete(string id);

	IReadOnlyList<ContractDto> GetItems(string watchlistId);

	/// <summary>Adds a listing; the same conId on another exchange is a separate item.</summary>
	void Add(string watchlistId, ContractDto contract);

	/// <summary>Removes the item for this listing (conId and exchange), its pin and its custom values.</summary>
	void Remove(string watchlistId, ContractDto contract);

	/// <summary>The pinned items, in the order they were pinned.</summary>
	IReadOnlyList<ContractDto> GetPinned(string watchlistId);

	/// <summary>Pins an item to the top, after those already pinned; pinning it again does nothing.</summary>
	void Pin(string watchlistId, ContractDto contract);

	/// <summary>Unpins an item; one that isn't pinned is left as it is.</summary>
	void Unpin(string watchlistId, ContractDto contract);

	/// <summary>The watchlist's custom columns in their order, each with its non-empty cells.</summary>
	IReadOnlyList<WatchlistColumnDto> GetColumns(string watchlistId);

	/// <summary>
	/// Adds a custom column (<c>ColumnId</c> null), after the others, or renames one. Names are
	/// unique per watchlist (case-insensitive); a watchlist has at most
	/// <c>WatchlistConstants.MaxCustomColumns</c>.
	/// </summary>
	WatchlistColumnDto SaveColumn(SaveWatchlistColumnRequest request);

	/// <summary>A free default name for a new custom column ("Column 1" …).</summary>
	string GetNewColumnName(string watchlistId);

	/// <summary>Deletes a custom column and its values; one that doesn't exist is ignored.</summary>
	void DeleteColumn(string watchlistId, string columnId);

	/// <summary>
	/// Sets an item's value in a custom column (trimmed; blank clears it). The item must be in
	/// the watchlist.
	/// </summary>
	void SetCell(SetWatchlistCellRequest request);

	/// <summary>
	/// The watchlist as a JSON file: items in saved order, pins, custom columns and their values.
	/// </summary>
	string Export(string watchlistId);

	/// <summary>
	/// Adds a watchlist read from a file and makes it the active one. An exported JSON file is
	/// checked whole and added as it was (items, pins, columns, values). Anything else is read as
	/// a list of symbols (one per line or comma-separated), checked, then looked up one at a
	/// time (IB's pacing), each as its first listing; symbols IB doesn't know are left out and
	/// returned. Nothing is added when the file is invalid or no symbol was found. The name is
	/// the file's (or <paramref name="fileName"/> without its extension), " (2)" … when taken.
	/// <see cref="Changed"/> is raised on the calling context once the watchlist is added.
	/// </summary>
	Task<WatchlistImportResultDto> ImportAsync(
		string content,
		string? fileName,
		CancellationToken cancellationToken = default
	);
}
