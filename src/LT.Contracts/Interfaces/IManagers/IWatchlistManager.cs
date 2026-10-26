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
	/// Raised on the calling thread after a watchlist is created, renamed or deleted, or its items
	/// or pins change, so other watchlist widgets showing it can follow.
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

	/// <summary>Removes the item for this listing (conId and exchange), and its pin.</summary>
	void Remove(string watchlistId, ContractDto contract);

	/// <summary>The pinned items, in the order they were pinned.</summary>
	IReadOnlyList<ContractDto> GetPinned(string watchlistId);

	/// <summary>Pins an item to the top, after those already pinned; pinning it again does nothing.</summary>
	void Pin(string watchlistId, ContractDto contract);

	/// <summary>Unpins an item; one that isn't pinned is left as it is.</summary>
	void Unpin(string watchlistId, ContractDto contract);
}
