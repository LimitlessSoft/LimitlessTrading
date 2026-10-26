using LT.Contracts.Interfaces.IManagers;
using Microsoft.Extensions.Hosting;

namespace LT.App.Services;

/// <summary>On start, off the UI thread: drops the old bar-cache folder and applies the cache size limit.</summary>
public class CacheMaintenance(ICacheManager cacheManager) : BackgroundService
{
	protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
		Task.Run(cacheManager.RunStartupMaintenance, stoppingToken);
}
