namespace LT.Contracts.Interfaces.IManagers;

/// <summary>
/// The Notes widgets' text, one note per id, kept on disk. Changes are written once typing has
/// paused for <c>NotesConstants.SaveDebounce</c>, or right away by <see cref="Flush"/> (a widget
/// closing, the app exiting). Notes are never deleted (only emptied ones): a widget that closes
/// leaves its note for the next one to open. Singleton, so it knows the notes shown in every
/// window and two widgets showing one note hear each other's edits.
/// </summary>
public interface INotesManager
{
	/// <summary>
	/// Raised on the calling thread with the note's id after its text changed, so other widgets
	/// showing it can follow.
	/// </summary>
	event Action<string>? Changed;

	/// <summary>
	/// A widget shows a note: <paramref name="noteId"/> (a saved widget's own), or with null the
	/// most recently edited saved note no open widget shows, else a new one. Returns its id; pair
	/// with <see cref="Close"/>.
	/// </summary>
	string Open(string? noteId = null);

	/// <summary>A widget no longer shows the note (closed, or switched to another).</summary>
	void Close(string noteId);

	/// <summary>The note's text; empty for a note that was never written (or emptied).</summary>
	string GetText(string noteId);

	/// <summary>Sets the note's text (empty removes it) and schedules writing it to disk.</summary>
	void SetText(string noteId, string text);

	/// <summary>Writes pending changes now; nothing when there are none.</summary>
	void Flush();
}
