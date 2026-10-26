using LT.Contracts.Dtos.PatchNotes;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;
using LT.Contracts.Interfaces.IManagers;
using LT.Contracts.Interfaces.IProcesses;
using LT.Contracts.Interfaces.IRepositories;

namespace LT.Domain.Managers;

/// <summary>
/// The patch notes: GitHub Releases' notes per version, cached in patch-notes.json so they open
/// offline and at once; refreshed whenever the patch notes window opens. Registered as a
/// singleton (see Program.cs).
/// </summary>
public class PatchNotesManager(
	IAppUpdater appUpdater,
	IPatchNotesRepository repository,
	TimeProvider timeProvider
) : IPatchNotesManager
{
	private readonly string _currentVersion = UpdateHelpers.NormalizeVersion(
		appUpdater.CurrentVersion
	);

	// One refresh at a time; a second caller returns right away
	private readonly SemaphoreSlim _gate = new(1, 1);

	// The saved state is read, changed and written back by both a refresh and TakeIsNewVersion
	private readonly Lock _stateLock = new();
	private bool _versionTaken;

	public event Action<PatchNotesStatusDto>? StatusChanged;

	public PatchNotesStatusDto Status
	{
		get =>
			field ??= PatchNotesHelpers.CreateStatus(
				PatchNotesState.Idle,
				_currentVersion,
				repository.Get()
			);
		private set
		{
			field = value;
			StatusChanged?.Invoke(value);
		}
	}

	public async Task RefreshAsync(CancellationToken cancellationToken = default)
	{
		if (!await _gate.WaitAsync(0, cancellationToken))
			return;
		try
		{
			Status = PatchNotesHelpers.CreateStatus(
				PatchNotesState.Loading,
				_currentVersion,
				repository.Get()
			);
			var releases = await appUpdater.GetReleasesAsync(cancellationToken);
			lock (_stateLock)
			{
				var state = repository.Get();
				state.Releases = releases.Select(PatchNotesHelpers.ToEntity).ToList();
				state.FetchedAt = timeProvider.GetUtcNow();
				repository.Save(state);
				Status = PatchNotesHelpers.CreateStatus(
					PatchNotesState.Loaded,
					_currentVersion,
					state
				);
			}
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			Status = PatchNotesHelpers.CreateStatus(
				PatchNotesState.Idle,
				_currentVersion,
				repository.Get()
			);
		}
		catch (Exception ex)
		{
			Status = PatchNotesHelpers.CreateStatus(
				PatchNotesState.Failed,
				_currentVersion,
				repository.Get(),
				$"Couldn't load the patch notes: {ex.Message}"
			);
		}
		finally
		{
			_gate.Release();
		}
	}

	public bool TakeIsNewVersion()
	{
		lock (_stateLock)
		{
			if (_versionTaken)
				return false;
			_versionTaken = true;
			var state = repository.Get();
			var isNew = PatchNotesHelpers.IsNewVersion(state.LastSeenVersion, _currentVersion);
			// Source builds aren't recorded: the next release start would count as an update
			if (
				!UpdateHelpers.IsDevVersion(_currentVersion)
				&& state.LastSeenVersion != _currentVersion
			)
			{
				state.LastSeenVersion = _currentVersion;
				try
				{
					repository.Save(state);
				}
				// Unsaved: the next start offers this version's notes again, nothing worse
				catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
			}
			return isNew;
		}
	}
}
