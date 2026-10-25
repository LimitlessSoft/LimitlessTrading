using LSCore.Validation.Contracts;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Updates;
using LT.Contracts.Enums;
using LT.Contracts.Enums.ValidationCodes;
using LT.Contracts.Helpers;
using LT.Contracts.Interfaces.IManagers;
using LT.Contracts.Interfaces.IProcesses;

namespace LT.Domain.Managers;

/// <summary>
/// Checks GitHub for a newer release and swaps it in on request. Registered as a singleton (see
/// Program.cs); the automatic checks are driven by LT.App's UpdateScheduler.
/// </summary>
public class UpdateManager(IAppUpdater appUpdater, IMessageLogManager messageLogManager)
	: IUpdateManager
{
	private readonly string _currentVersion = UpdateHelpers.NormalizeVersion(
		appUpdater.CurrentVersion
	);

	// One check or install at a time; a second caller returns right away
	private readonly SemaphoreSlim _gate = new(1, 1);
	private AppReleaseDto? _latest;

	public event Action<UpdateStatusDto>? StatusChanged;

	public UpdateStatusDto Status
	{
		get =>
			field ??= UpdateHelpers.CreateStatus(
				UpdateState.Idle,
				_currentVersion,
				null,
				appUpdater.CannotInstallReason
			);
		private set
		{
			field = value;
			StatusChanged?.Invoke(value);
		}
	}

	public async Task CheckAsync(CancellationToken cancellationToken = default)
	{
		if (!await _gate.WaitAsync(0, cancellationToken))
			return;
		try
		{
			// A downloaded update waits for the restart; checking again would hide it
			if (Status.State == UpdateState.Restarting)
				return;
			Status = UpdateHelpers.CreateStatus(
				UpdateState.Checking,
				_currentVersion,
				_latest,
				appUpdater.CannotInstallReason
			);
			var release = await appUpdater.GetLatestReleaseAsync(cancellationToken);
			var announce = false;
			if (UpdateHelpers.IsNewer(release.Tag, _currentVersion))
			{
				announce = _latest?.Tag != release.Tag;
				_latest = release;
			}
			else
				_latest = null;

			Status = UpdateHelpers.CreateStatus(
				_latest is null ? UpdateState.UpToDate : UpdateState.Available,
				_currentVersion,
				_latest,
				appUpdater.CannotInstallReason
			);
			if (announce)
				messageLogManager.Add(
					MessageSeverity.Info,
					MessageLogConstants.SourceUpdates,
					$"Version {Status.LatestVersion} is available (running {_currentVersion})."
				);
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			Status = UpdateHelpers.CreateStatus(
				_latest is null ? UpdateState.Idle : UpdateState.Available,
				_currentVersion,
				_latest,
				appUpdater.CannotInstallReason
			);
		}
		catch (Exception ex)
		{
			var error = $"Update check failed: {ex.Message}";
			messageLogManager.Add(MessageSeverity.Error, MessageLogConstants.SourceUpdates, error);
			Status = UpdateHelpers.CreateStatus(
				UpdateState.Failed,
				_currentVersion,
				_latest,
				appUpdater.CannotInstallReason,
				error: error
			);
		}
		finally
		{
			_gate.Release();
		}
	}

	public async Task InstallAsync(CancellationToken cancellationToken = default)
	{
		if (!await _gate.WaitAsync(0, cancellationToken))
			return;
		try
		{
			if (_latest is not { } release || Status.State == UpdateState.Restarting)
				return;
			var cannotInstallReason = appUpdater.CannotInstallReason;
			var asset = UpdateHelpers.SelectAsset(release.Assets, appUpdater.AssetSuffix);
			var error =
				cannotInstallReason
				?? (
					asset is null
						? UpdateValidationCodes.UVC_004.GetValidationMessage(appUpdater.AssetSuffix)
						: null
				);
			if (error != null || asset is null)
				throw new InvalidOperationException(error);

			Status = UpdateHelpers.CreateStatus(
				UpdateState.Downloading,
				_currentVersion,
				release,
				cannotInstallReason,
				progress: 0
			);
			// Reports may still arrive after the download finished: only Downloading takes them
			var progress = new Progress<double>(value =>
			{
				if (Status.State == UpdateState.Downloading)
					Status = UpdateHelpers.CreateStatus(
						UpdateState.Downloading,
						_currentVersion,
						release,
						cannotInstallReason,
						progress: value
					);
			});
			await appUpdater.DownloadAsync(asset, progress, cancellationToken);
			appUpdater.ApplyAndRestart();
			Status = UpdateHelpers.CreateStatus(
				UpdateState.Restarting,
				_currentVersion,
				release,
				cannotInstallReason
			);
			messageLogManager.Add(
				MessageSeverity.Info,
				MessageLogConstants.SourceUpdates,
				$"Restarting into version {Status.LatestVersion}."
			);
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			Status = UpdateHelpers.CreateStatus(
				UpdateState.Available,
				_currentVersion,
				_latest,
				appUpdater.CannotInstallReason
			);
		}
		catch (Exception ex)
		{
			var error = $"Update failed: {ex.Message}";
			messageLogManager.Add(MessageSeverity.Error, MessageLogConstants.SourceUpdates, error);
			Status = UpdateHelpers.CreateStatus(
				UpdateState.Failed,
				_currentVersion,
				_latest,
				appUpdater.CannotInstallReason,
				error: error
			);
		}
		finally
		{
			_gate.Release();
		}
	}
}
