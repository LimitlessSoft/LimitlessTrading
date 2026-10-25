using LT.Contracts.Constants;
using LT.Contracts.Helpers;
using LT.Contracts.Interfaces.IManagers;
using LT.Contracts.Interfaces.IProcesses;
using Microsoft.Extensions.Hosting;

namespace LT.App.Services;

/// <summary>
/// Clears what the last update left behind, then checks for a newer release shortly after start
/// and every few hours while "Check for updates" is on. Builds from source never check by
/// themselves (the menu's Check for updates still works).
/// </summary>
public class UpdateScheduler(
	IUpdateManager updateManager,
	ISettingsManager settingsManager,
	IAppUpdater appUpdater,
	TimeProvider timeProvider
) : BackgroundService
{
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		appUpdater.CleanUp();
		if (UpdateHelpers.IsDevVersion(appUpdater.CurrentVersion))
			return;
		try
		{
			await Task.Delay(AppConstants.UpdateStartupDelay, timeProvider, stoppingToken);
			while (!stoppingToken.IsCancellationRequested)
			{
				if (settingsManager.GetAppSettings().CheckForUpdates)
					await updateManager.CheckAsync(stoppingToken);
				await Task.Delay(AppConstants.UpdateCheckInterval, timeProvider, stoppingToken);
			}
		}
		catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
	}
}
