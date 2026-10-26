using LT.Contracts.Entities;

namespace LT.Contracts.Interfaces.IRepositories;

public interface IFlexImportStateRepository
{
	/// <summary>The saved state; a new one when nothing was imported yet.</summary>
	FlexImportStateEntity Get();
	void Save(FlexImportStateEntity state);
}
