using LT.Contracts.Dtos.Settings;
using LT.Contracts.Helpers;
using LT.Contracts.Interfaces.IManagers;
using LT.Contracts.Interfaces.INewsSources;
using Microsoft.Extensions.Hosting;

namespace LT.App.Services;

/// <summary>
/// Keeps the default broad feeds (SEC filings, PR Newswire, Fed, BLS) polled the whole time the
/// app runs, with or without a News widget, so the archive has no gaps while the app is up; stops
/// and starts with the <c>NewsFeeds</c> setting. Applies the archive's retention on start and
/// when the setting changes.
/// </summary>
public class NewsFeedCollector(
	INewsFeedPoller poller,
	INewsArchiveManager archiveManager,
	ISettingsManager settingsManager
) : BackgroundService
{
	private readonly Lock _lock = new();
	private CancellationTokenSource? _collecting;
	private CancellationToken _stopping;
	private int? _retentionDays;

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		_stopping = stoppingToken;
		settingsManager.AppSettingsChanged += Apply;
		Apply(settingsManager.GetAppSettings());
		try
		{
			await Task.Delay(Timeout.Infinite, stoppingToken);
		}
		catch (OperationCanceledException) { }
		finally
		{
			settingsManager.AppSettingsChanged -= Apply;
			lock (_lock)
				_collecting?.Cancel();
		}
	}

	private void Apply(AppSettingsDto settings)
	{
		if (settings.NewsRetentionDays != _retentionDays)
		{
			_retentionDays = settings.NewsRetentionDays;
			_ = Task.Run(archiveManager.ApplyRetention, _stopping);
		}
		lock (_lock)
		{
			if (settings.NewsFeeds == (_collecting != null) || _stopping.IsCancellationRequested)
				return;
			if (!settings.NewsFeeds)
			{
				_collecting!.Cancel();
				_collecting = null;
				return;
			}
			_collecting = CancellationTokenSource.CreateLinkedTokenSource(_stopping);
			var token = _collecting.Token;
			foreach (var feed in NewsFeedHelpers.GetBackgroundFeeds())
				_ = Task.Run(
					async () =>
					{
						try
						{
							// Archived by the poller; nothing else to do with them here
							await foreach (var _ in poller.WatchAsync(feed.Code, null, token)) { }
						}
						catch (OperationCanceledException) { }
					},
					token
				);
		}
	}
}
