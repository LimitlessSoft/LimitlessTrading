using LT.Contracts.Entities;

namespace LT.Contracts.Helpers;

public static class NotesHelpers
{
	/// <summary>The id of a new note (a new Notes widget).</summary>
	public static string NewId() => Guid.NewGuid().ToString("N");

	/// <summary>
	/// The note a new Notes widget opens on: the most recently edited saved note that no open
	/// widget shows (<paramref name="shown"/>), so a closed widget's text comes back when Notes is
	/// opened again; null when every saved note is on screen (a new note then).
	/// </summary>
	public static string? PickUnshown(NotesEntity notes, IReadOnlyCollection<string> shown) =>
		notes
			.Notes.Where(x => !shown.Contains(x.Key))
			.OrderByDescending(x => x.Value.UpdatedAt)
			.ThenBy(x => x.Key, StringComparer.Ordinal)
			.Select(x => x.Key)
			.FirstOrDefault();

	public static string GetText(NotesEntity notes, string noteId) =>
		notes.Notes.TryGetValue(noteId, out var note) ? note.Text : string.Empty;

	/// <summary>
	/// Sets the note's text, stamped <paramref name="now"/>; an empty text removes the note.
	/// False when nothing changed.
	/// </summary>
	public static bool SetText(NotesEntity notes, string noteId, string text, DateTimeOffset now)
	{
		if (GetText(notes, noteId) == text)
			return false;
		if (text.Length == 0)
			notes.Notes.Remove(noteId);
		else
			notes.Notes[noteId] = new NoteEntity { Text = text, UpdatedAt = now };
		return true;
	}

	/// <summary>A copy to write while the original keeps changing.</summary>
	public static NotesEntity Copy(NotesEntity notes) =>
		new()
		{
			Notes = notes.Notes.ToDictionary(
				x => x.Key,
				x => new NoteEntity { Text = x.Value.Text, UpdatedAt = x.Value.UpdatedAt }
			),
		};
}
