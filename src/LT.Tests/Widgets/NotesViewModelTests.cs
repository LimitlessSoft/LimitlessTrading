using LT.App.Widgets.Notes;
using LT.Contracts.Dtos.Settings;
using LT.Domain.Managers;
using LT.Tests.Fakes;
using Microsoft.Extensions.Time.Testing;

namespace LT.Tests.Widgets;

/// <summary>
/// Every Notes widget has a note of its own, kept by id in its saved placement; two widgets on one
/// note (a duplicated view) follow each other's edits. Notes are never lost: a new widget opens on
/// the last edited note no open widget shows, so closing one and opening Notes brings it back.
/// </summary>
public class NotesViewModelTests
{
	private readonly InMemoryNotesRepository _repository = new();
	private readonly NotesManager _manager;

	private readonly FakeTimeProvider _time = new();

	public NotesViewModelTests() =>
		_manager = new NotesManager(_repository, new MessageLogManager(), _time);

	private static string? SavedNoteId(NotesViewModel widget)
	{
		var placement = new WidgetPlacementDto();
		widget.SaveState(placement);
		return placement.NoteId;
	}

	[Fact]
	public void New_widgets_start_empty_on_notes_of_their_own()
	{
		var first = new NotesViewModel(_manager);
		var second = new NotesViewModel(_manager);

		first.Text = "only here";

		Assert.Equal(string.Empty, second.Text);
		Assert.NotEqual(SavedNoteId(first), SavedNoteId(second));
	}

	[Fact]
	public void A_saved_widget_shows_its_note_again()
	{
		var widget = new NotesViewModel(_manager);
		widget.Text = "remember this";
		var placement = new WidgetPlacementDto();
		widget.SaveState(placement);
		widget.Dispose();

		var restored = new NotesViewModel(
			new NotesManager(_repository, new MessageLogManager(), new FakeTimeProvider())
		);
		restored.LoadState(placement);

		Assert.Equal("remember this", restored.Text);
	}

	[Fact]
	public void Closing_the_widget_writes_its_note_at_once()
	{
		var widget = new NotesViewModel(_manager);
		widget.Text = "typed just now";

		widget.Dispose();

		Assert.Equal("typed just now", Assert.Single(_repository.Saved!.Notes).Value.Text);
	}

	[Fact]
	public void Two_widgets_on_one_note_follow_each_others_edits()
	{
		var first = new NotesViewModel(_manager);
		var copy = new NotesViewModel(_manager);
		var placement = new WidgetPlacementDto();
		first.SaveState(placement);
		copy.LoadState(placement);

		first.Text = "shared";
		Assert.Equal("shared", copy.Text);

		copy.Text = "shared, edited";
		Assert.Equal("shared, edited", first.Text);
	}

	[Fact]
	public void Reopening_notes_after_closing_brings_the_text_back()
	{
		var widget = new NotesViewModel(_manager);
		widget.Text = "don't lose me";
		widget.Dispose();

		var reopened = new NotesViewModel(_manager);

		Assert.Equal("don't lose me", reopened.Text);
		Assert.Equal(_repository.Saved!.Notes.Keys.Single(), SavedNoteId(reopened));
	}

	[Fact]
	public void Reopening_after_a_restart_picks_the_last_edited_unshown_note()
	{
		var first = new NotesViewModel(_manager);
		first.Text = "older";
		_time.Advance(TimeSpan.FromMinutes(1));
		var second = new NotesViewModel(_manager);
		second.Text = "newer";
		first.Dispose();
		second.Dispose();

		var restarted = new NotesManager(_repository, new MessageLogManager(), _time);
		var a = new NotesViewModel(restarted);
		var b = new NotesViewModel(restarted);

		Assert.Equal(("newer", "older"), (a.Text, b.Text));
	}

	[Fact]
	public void Two_open_widgets_get_two_different_saved_notes()
	{
		var first = new NotesViewModel(_manager);
		first.Text = "one";
		var second = new NotesViewModel(_manager);
		second.Text = "two";
		first.Dispose();
		second.Dispose();

		var a = new NotesViewModel(_manager);
		var b = new NotesViewModel(_manager);

		Assert.NotEqual(SavedNoteId(a), SavedNoteId(b));
		Assert.Equal(["one", "two"], new[] { a.Text, b.Text }.Order());
	}

	[Fact]
	public void With_every_saved_note_on_screen_a_new_widget_starts_a_new_note()
	{
		var open = new NotesViewModel(_manager);
		open.Text = "on screen";

		var added = new NotesViewModel(_manager);

		Assert.Equal(string.Empty, added.Text);
		Assert.NotEqual(SavedNoteId(open), SavedNoteId(added));
	}

	[Fact]
	public void A_restored_widget_leaves_the_note_it_first_picked_for_others()
	{
		var saved = new NotesViewModel(_manager);
		saved.Text = "mine";
		var placement = new WidgetPlacementDto();
		saved.SaveState(placement);
		_time.Advance(TimeSpan.FromMinutes(1));
		var other = new NotesViewModel(_manager);
		other.Text = "other";
		saved.Dispose();
		other.Dispose();

		// Restoring: the view model is created first (it picks "other", the newest), then loaded
		var restored = new NotesViewModel(_manager);
		restored.LoadState(placement);
		var reopened = new NotesViewModel(_manager);

		Assert.Equal(("mine", "other"), (restored.Text, reopened.Text));
	}
}
