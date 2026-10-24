using LT.Contracts.Dtos.Contracts;

namespace LT.Contracts.Interfaces.IManagers;

public interface IWatchlistManager
{
	IReadOnlyList<ContractDto> GetItems();
	void Add(ContractDto contract);
	void Remove(int conId);
}
