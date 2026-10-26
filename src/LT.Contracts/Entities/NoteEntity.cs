namespace LT.Contracts.Entities;

public class NoteEntity
{
	public string Text { get; set; } = string.Empty;
	public DateTimeOffset UpdatedAt { get; set; }
}
