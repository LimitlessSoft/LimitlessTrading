using LT.Contracts.Constants;
using LT.Domain.Managers;
using LT.Tests.Fakes;
using Microsoft.Extensions.Time.Testing;

namespace LT.Tests.Domain.Managers;

/// <summary>
/// Notes are kept in memory and written to disk once typing pauses (debounced), or right away on
/// flush (a widget closing) and when the manager is disposed (the app exiting).
/// </summary>
public class NotesManagerTests
{
	private readonly InMemoryNotesRepository _repository = new();
	private readonly FakeTimeProvider _time = new(DateTimeOffset.Parse("2026-10-26T12:00:00Z"));
	private readonly MessageLogManager _messageLog = new();

	private NotesManager CreateManager() => new(_repository, _messageLog, _time);

	[Fact]
	public void SetText_is_written_only_after_typing_pauses()
	{
		var manager = CreateManager();

		manager.SetText("n1", "a");
		_time.Advance(NotesConstants.SaveDebounce / 2);
		var lastEdit = _time.GetUtcNow();
		manager.SetText("n1", "ab");
		_time.Advance(NotesConstants.SaveDebounce / 2);
		Assert.Null(_repository.Saved);

		_time.Advance(NotesConstants.SaveDebounce / 2);
		Assert.Equal(1, _repository.SaveCount);
		Assert.Equal("ab", _repository.Saved!.Notes["n1"].Text);
		Assert.Equal(lastEdit, _repository.Saved.Notes["n1"].UpdatedAt);
	}

	[Fact]
	public void Flush_writes_pending_changes_at_once_and_nothing_twice()
	{
		var manager = CreateManager();
		manager.SetText("n1", "hello");

		manager.Flush();
		manager.Flush();
		_time.Advance(NotesConstants.SaveDebounce * 2);

		Assert.Equal(1, _repository.SaveCount);
		Assert.Equal("hello", _repository.Saved!.Notes["n1"].Text);
	}

	[Fact]
	public void Dispose_writes_pending_changes()
	{
		var manager = CreateManager();
		manager.SetText("n1", "before exit");

		manager.Dispose();

		Assert.Equal("before exit", _repository.Saved!.Notes["n1"].Text);
	}

	[Fact]
	public void Notes_are_restored_by_a_new_manager()
	{
		var manager = CreateManager();
		manager.SetText("n1", "first");
		manager.SetText("n2", "second");
		manager.Flush();

		var restarted = CreateManager();

		Assert.Equal("first", restarted.GetText("n1"));
		Assert.Equal("second", restarted.GetText("n2"));
		Assert.Equal(string.Empty, restarted.GetText("unknown"));
	}

	[Fact]
	public void An_emptied_note_is_removed()
	{
		var manager = CreateManager();
		manager.SetText("n1", "text");
		manager.SetText("n1", string.Empty);
		manager.Flush();

		Assert.Empty(_repository.Saved!.Notes);
	}

	[Fact]
	public void Changed_is_raised_for_a_real_change_only()
	{
		var manager = CreateManager();
		var changed = new List<string>();
		manager.Changed += changed.Add;

		manager.SetText("n1", "x");
		manager.SetText("n1", "x");
		manager.SetText("n2", string.Empty);

		Assert.Equal(["n1"], changed);
	}

	[Fact]
	public void A_failed_write_is_logged_and_retried_on_the_next_flush()
	{
		var manager = CreateManager();
		manager.SetText("n1", "keep me");
		_repository.FailSaves = true;

		manager.Flush();

		Assert.Null(_repository.Saved);
		Assert.Contains(_messageLog.GetAll(), x => x.Source == MessageLogConstants.SourceNotes);

		_repository.FailSaves = false;
		manager.Flush();
		Assert.Equal("keep me", _repository.Saved!.Notes["n1"].Text);
	}

	[Fact]
	public void Open_returns_the_last_edited_unshown_note_and_a_new_one_once_all_are_shown()
	{
		var manager = CreateManager();
		manager.SetText("old", "a");
		_time.Advance(TimeSpan.FromMinutes(1));
		manager.SetText("new", "b");

		Assert.Equal("new", manager.Open());
		Assert.Equal("old", manager.Open());
		var fresh = manager.Open();
		Assert.NotEqual("new", fresh);
		Assert.NotEqual("old", fresh);

		manager.Close("old");
		Assert.Equal("old", manager.Open());
	}

	[Fact]
	public void A_note_shown_twice_stays_shown_until_both_close()
	{
		var manager = CreateManager();
		manager.SetText("n1", "shared");
		manager.Open("n1");
		manager.Open("n1");

		manager.Close("n1");
		Assert.NotEqual("n1", manager.Open());

		manager.Close("n1");
		Assert.Equal("n1", manager.Open());
	}
}
