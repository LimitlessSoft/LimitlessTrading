namespace LT.Contracts.Entities;

/// <summary>Every Notes widget's text (notes.json), by note id; an empty note isn't kept.</summary>
public class NotesEntity
{
	public Dictionary<string, NoteEntity> Notes { get; set; } = [];
}
