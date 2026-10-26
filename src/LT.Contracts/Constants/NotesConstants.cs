namespace LT.Contracts.Constants;

public static class NotesConstants
{
	/// <summary>Every note, in the app-data folder.</summary>
	public const string FileName = "notes.json";

	/// <summary>Typing has to pause this long before the notes are written to disk.</summary>
	public static readonly TimeSpan SaveDebounce = TimeSpan.FromSeconds(1);
}
