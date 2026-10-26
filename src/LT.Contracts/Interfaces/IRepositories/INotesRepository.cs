using LT.Contracts.Entities;

namespace LT.Contracts.Interfaces.IRepositories;

public interface INotesRepository
{
	/// <summary>The saved notes; none when nothing was saved yet or the file is unreadable.</summary>
	NotesEntity Get();
	void Save(NotesEntity notes);
}
