using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.Watchlists;
using LT.Contracts.Requests.Watchlists;

namespace LT.Contracts.Interfaces.IManagers;

/// <summary>Named watchlists; one is active, and items are read and changed on that one.</summary>
public interface IWatchlistManager
{
	IReadOnlyList<WatchlistDto> GetWatchlists();
	WatchlistDto GetActive();
	void SetActive(string id);

	/// <summary>Creates a watchlist (<c>Id</c> null) or renames one; the new one becomes active.</summary>
	WatchlistDto SaveName(SaveWatchlistNameRequest request);

	/// <summary>A free default name for a new watchlist ("Watchlist 2" …).</summary>
	string GetNewName();

	/// <summary>Deletes a watchlist and its items; the last one can't be deleted.</summary>
	void Delete(string id);

	IReadOnlyList<ContractDto> GetItems();

	/// <summary>Adds a listing; the same conId on another exchange is a separate item.</summary>
	void Add(ContractDto contract);

	/// <summary>Removes the item for this listing (conId and exchange), and its pin.</summary>
	void Remove(ContractDto contract);

	/// <summary>The pinned items, in the order they were pinned.</summary>
	IReadOnlyList<ContractDto> GetPinned();

	/// <summary>Pins an item to the top, after those already pinned; pinning it again does nothing.</summary>
	void Pin(ContractDto contract);

	/// <summary>Unpins an item; one that isn't pinned is left as it is.</summary>
	void Unpin(ContractDto contract);
}
