using LT.Contracts.Constants;
using LT.Contracts.Enums;
using LT.Contracts.Interfaces.IManagers;
using Microsoft.Extensions.Hosting;

namespace LT.App.Services;

/// <summary>
/// Drives IConnectionManager.CheckHealthAsync on the configured heartbeat interval. There is no
/// resume on start: the password isn't stored, so the user logs in to IB Gateway each session.
/// </summary>
public class ConnectionWatchdog(
	IConnectionManager connectionManager,
	ISettingsManager settingsManager,
	IMessageLogManager messageLogManager
) : BackgroundService
{
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				await Task.Delay(
					TimeSpan.FromSeconds(settingsManager.GetAppSettings().HeartbeatSeconds),
					stoppingToken
				);
				await connectionManager.CheckHealthAsync(stoppingToken);
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
			{
				return;
			}
			catch (Exception ex)
			{
				messageLogManager.Add(
					MessageSeverity.Error,
					MessageLogConstants.SourceConnection,
					$"Watchdog: {ex.Message}"
				);
			}
		}
	}
}
