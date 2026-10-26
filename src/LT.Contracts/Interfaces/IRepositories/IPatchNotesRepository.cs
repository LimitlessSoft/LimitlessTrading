using LT.Contracts.Entities;

namespace LT.Contracts.Interfaces.IRepositories;

public interface IPatchNotesRepository
{
	/// <summary>The saved state; a new one when nothing was saved yet or the file is unreadable.</summary>
	PatchNotesStateEntity Get();
	void Save(PatchNotesStateEntity state);
}
