using LT.Contracts.Interfaces.IManagers;
using Microsoft.Extensions.Hosting;

namespace LT.App.Services;

/// <summary>
/// Sets up the indicators folder, compiles its scripts and recompiles them as they change, for
/// the app's whole lifetime.
/// </summary>
public class IndicatorScriptHost(IIndicatorManager indicatorManager) : BackgroundService
{
	protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
		Task.Run(() => indicatorManager.RunAsync(stoppingToken), stoppingToken);
}
