using CommunityToolkit.Mvvm.ComponentModel;
using LT.App.ViewModels;
using LT.App.Workspace;
using LT.Contracts.Dtos.Settings;
using LT.Contracts.Interfaces.IManagers;

namespace LT.App.Widgets.Notes;

/// <summary>
/// A free-text notes pad; needs no connection. Every widget has a note of its own (its id saved
/// in the layout); a new widget opens on the last edited note no other widget shows (a closed
/// widget's), else a new one. A duplicated view's copy shows the same note and follows its edits.
/// Typing is saved by <see cref="INotesManager"/> once it pauses, and right away when the widget
/// closes.
/// </summary>
public partial class NotesViewModel : ViewModelBase, IWidgetState, IDisposable
{
	private readonly INotesManager _notesManager;

	// The note this widget shows
	private string _noteId;

	// Text is being set from the note, not typed: not written back
	private bool _loading;

	public NotesViewModel(INotesManager notesManager)
	{
		_notesManager = notesManager;
		_noteId = notesManager.Open();
		ShowNote();
		notesManager.Changed += OnNoteChanged;
	}

	[ObservableProperty]
	public partial string Text { get; set; } = string.Empty;

	/// <summary>A saved widget shows its own note again.</summary>
	public void LoadState(WidgetPlacementDto placement)
	{
		if (placement.NoteId is not { Length: > 0 } id || id == _noteId)
			return;
		_notesManager.Close(_noteId);
		_noteId = _notesManager.Open(id);
		ShowNote();
	}

	public void SaveState(WidgetPlacementDto placement) => placement.NoteId = _noteId;

	/// <summary>The widget is closed (or its window discarded): writes what is still pending.</summary>
	public void Dispose()
	{
		_notesManager.Changed -= OnNoteChanged;
		_notesManager.Close(_noteId);
		_notesManager.Flush();
	}

	partial void OnTextChanged(string value)
	{
		if (!_loading)
			_notesManager.SetText(_noteId, value ?? string.Empty);
	}

	// Another widget showing this note edited it (this widget's own edits find the text equal)
	private void OnNoteChanged(string noteId)
	{
		if (noteId == _noteId)
			ShowNote();
	}

	private void ShowNote()
	{
		var text = _notesManager.GetText(_noteId);
		if (text == Text)
			return;
		_loading = true;
		Text = text;
		_loading = false;
	}
}
