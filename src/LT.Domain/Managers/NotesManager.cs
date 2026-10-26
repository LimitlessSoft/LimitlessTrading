using LT.Contracts.Constants;
using LT.Contracts.Entities;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;
using LT.Contracts.Interfaces.IManagers;
using LT.Contracts.Interfaces.IRepositories;

namespace LT.Domain.Managers;

/// <summary>
/// Holds every note in memory (read once, on first use) and writes them all
/// <see cref="NotesConstants.SaveDebounce"/> after the last change, on a timer thread. Disposed
/// with the host when the app exits, which writes what is still pending.
/// </summary>
public class NotesManager(
	INotesRepository repository,
	IMessageLogManager messageLogManager,
	TimeProvider timeProvider
) : INotesManager, IDisposable
{
	private readonly Lock _lock = new();

	// Writes one at a time, in order, so an older copy never lands after a newer one
	private readonly Lock _saveLock = new();
	private NotesEntity? _notes;

	// Notes open widgets show, with how many show each (a duplicated view's copy shares one)
	private readonly Dictionary<string, int> _shown = [];
	private ITimer? _timer;
	private bool _dirty;

	public event Action<string>? Changed;

	public string Open(string? noteId = null)
	{
		lock (_lock)
		{
			var id =
				noteId
				?? NotesHelpers.PickUnshown(_notes ??= repository.Get(), _shown.Keys)
				?? NotesHelpers.NewId();
			_shown[id] = _shown.GetValueOrDefault(id) + 1;
			return id;
		}
	}

	public void Close(string noteId)
	{
		lock (_lock)
		{
			if (!_shown.TryGetValue(noteId, out var count))
				return;
			if (count > 1)
				_shown[noteId] = count - 1;
			else
				_shown.Remove(noteId);
		}
	}

	public string GetText(string noteId)
	{
		lock (_lock)
			return NotesHelpers.GetText(_notes ??= repository.Get(), noteId);
	}

	public void SetText(string noteId, string text)
	{
		lock (_lock)
		{
			if (
				!NotesHelpers.SetText(
					_notes ??= repository.Get(),
					noteId,
					text,
					timeProvider.GetUtcNow()
				)
			)
				return;
			_dirty = true;
			if (_timer == null)
				_timer = timeProvider.CreateTimer(
					_ => Flush(),
					null,
					NotesConstants.SaveDebounce,
					Timeout.InfiniteTimeSpan
				);
			else
				_timer.Change(NotesConstants.SaveDebounce, Timeout.InfiniteTimeSpan);
		}
		Changed?.Invoke(noteId);
	}

	public void Flush()
	{
		lock (_saveLock)
		{
			NotesEntity copy;
			lock (_lock)
			{
				if (!_dirty || _notes == null)
					return;
				copy = NotesHelpers.Copy(_notes);
				_dirty = false;
			}
			try
			{
				repository.Save(copy);
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				// Still pending: written with the next change or flush
				lock (_lock)
					_dirty = true;
				messageLogManager.Add(
					MessageSeverity.Error,
					MessageLogConstants.SourceNotes,
					$"Couldn't save the notes: {ex.Message}"
				);
			}
		}
	}

	public void Dispose()
	{
		lock (_lock)
		{
			_timer?.Dispose();
			_timer = null;
		}
		Flush();
	}
}
