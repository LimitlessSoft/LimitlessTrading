using LT.Contracts.Entities;

namespace LT.Contracts.Interfaces.IRepositories;

/// <summary>SEC shares-outstanding lookups by ticker, whatever their age.</summary>
public interface ISharesOutstandingRepository
{
	SharesOutstandingEntity? Get(string ticker);
	void Save(string ticker, SharesOutstandingEntity entry);
}
