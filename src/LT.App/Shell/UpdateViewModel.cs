using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LT.App.ViewModels;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Updates;
using LT.Contracts.Enums;
using LT.Contracts.Interfaces.IManagers;

namespace LT.App.Shell;

/// <summary>
/// The title bar's "Update" button and its flyout: shown once a newer release is known, installs
/// it on request and asks the window to close when the new version is ready to start.
/// </summary>
public partial class UpdateViewModel : ViewModelBase
{
	private readonly IUpdateManager _updateManager;
	private readonly IMessageLogManager _messageLogManager;

	public UpdateViewModel(IUpdateManager updateManager, IMessageLogManager messageLogManager)
	{
		_updateManager = updateManager;
		_messageLogManager = messageLogManager;
		Apply(updateManager.Status);
		updateManager.StatusChanged += status => Dispatcher.UIThread.Post(() => Apply(status));
	}

	/// <summary>Raised on the UI thread once the new version waits for this one to exit.</summary>
	public event Action? RestartRequested;

	[ObservableProperty]
	public partial bool IsVisible { get; set; }

	[ObservableProperty]
	public partial string ButtonText { get; set; } = string.Empty;

	[ObservableProperty]
	public partial string VersionsText { get; set; } = string.Empty;

	[ObservableProperty]
	public partial string? ReleasePageUrl { get; set; }

	[ObservableProperty]
	public partial string? CannotInstallReason { get; set; }

	[ObservableProperty]
	public partial string? ErrorText { get; set; }

	[ObservableProperty]
	public partial bool IsDownloading { get; set; }

	[ObservableProperty]
	public partial double Progress { get; set; }

	[ObservableProperty]
	[NotifyCanExecuteChangedFor(nameof(InstallCommand))]
	public partial bool CanInstall { get; set; }

	[RelayCommand(CanExecute = nameof(CanInstall))]
	private Task InstallAsync() => _updateManager.InstallAsync();

	/// <summary>The menu's Check for updates: also says so when there is nothing newer.</summary>
	[RelayCommand]
	private async Task CheckAsync()
	{
		await _updateManager.CheckAsync();
		if (_updateManager.Status.State == UpdateState.UpToDate)
			_messageLogManager.Add(
				MessageSeverity.Info,
				MessageLogConstants.SourceUpdates,
				$"You're on the latest version ({_updateManager.Status.CurrentVersion})."
			);
	}

	private void Apply(UpdateStatusDto status)
	{
		IsVisible = status.LatestVersion != null;
		IsDownloading = status.State == UpdateState.Downloading;
		Progress = (status.Progress ?? 0) * 100;
		ButtonText = status.State switch
		{
			UpdateState.Downloading => $"Updating {Progress:0}%",
			UpdateState.Restarting => "Restarting…",
			_ => $"Update {status.LatestVersion}",
		};
		VersionsText =
			$"Version {status.LatestVersion} is available — you have {status.CurrentVersion}.";
		ReleasePageUrl = status.ReleasePageUrl;
		CannotInstallReason = status.CannotInstallReason;
		ErrorText = status.State == UpdateState.Failed ? status.Error : null;
		CanInstall =
			status.CannotInstallReason is null
			&& status.State is UpdateState.Available or UpdateState.Failed;
		if (status.State == UpdateState.Restarting)
			RestartRequested?.Invoke();
	}
}
