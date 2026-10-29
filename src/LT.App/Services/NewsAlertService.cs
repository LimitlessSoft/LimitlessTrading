using LT.Contracts.Dtos.News;
using LT.Contracts.Interfaces.IManagers;
using Microsoft.Extensions.Hosting;

namespace LT.App.Services;

/// <summary>
/// Hands every headline the archive sees for the first time (any source: the feeds collected in
/// the background, IB's while a News widget streams them) to <see cref="INewsAlertManager"/>,
/// which plays the news sound for a recent match.
/// </summary>
public class NewsAlertService(
	INewsArchiveManager archiveManager,
	INewsAlertManager newsAlertManager
) : BackgroundService
{
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		archiveManager.HeadlinesAdded += OnHeadlinesAdded;
		try
		{
			await Task.Delay(Timeout.Infinite, stoppingToken);
		}
		catch (OperationCanceledException) { }
		finally
		{
			archiveManager.HeadlinesAdded -= OnHeadlinesAdded;
		}
	}

	private void OnHeadlinesAdded(IReadOnlyList<NewsHeadlineDto> headlines) =>
		newsAlertManager.OnHeadlines(headlines);
}
