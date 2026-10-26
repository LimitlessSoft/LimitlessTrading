using LT.Contracts.Interfaces.IManagers;
using Microsoft.Extensions.Hosting;

namespace LT.App.Services;

/// <summary>
/// Runs the IB Flex import for the app's whole lifetime (back-fill, daily Activity statement,
/// Trade Confirmation every few minutes), connected to IB Gateway or not — Flex is plain HTTPS.
/// </summary>
public class FlexImportScheduler(IFlexImportManager flexImportManager) : BackgroundService
{
	protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
		flexImportManager.RunAsync(stoppingToken);
}
