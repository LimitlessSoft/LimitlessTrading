using LT.Contracts.Interfaces.IManagers;
using Microsoft.Extensions.Hosting;

namespace LT.App.Services;

/// <summary>
/// Runs the account performance recording for the app's whole lifetime, so history is kept
/// whenever the app is connected, whether or not the Account performance widget is open.
/// </summary>
public class AccountPerformanceRecorder(IAccountPerformanceManager accountPerformanceManager)
	: BackgroundService
{
	protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
		accountPerformanceManager.RecordAsync(stoppingToken);
}
