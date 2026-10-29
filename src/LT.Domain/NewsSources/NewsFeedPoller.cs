using System.Runtime.CompilerServices;
using System.Threading.Channels;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Feeds;
using LT.Contracts.Dtos.News;
using LT.Contracts.Enums;
using LT.Contracts.Exceptions;
using LT.Contracts.Helpers;
using LT.Contracts.Interfaces.IApiClients;
using LT.Contracts.Interfaces.IManagers;
using LT.Contracts.Interfaces.INewsSources;

namespace LT.Domain.NewsSources;

/// <summary>
/// One polling loop per feed URL (<see cref="INewsFeedPoller"/>), ref-counted by its watchers and
/// timed with <see cref="TimeProvider"/>. A poll sends the last ETag / Last-Modified, so an
/// unchanged feed is a 304. SEC entries are tagged by CIK with the SEC's ticker file, read once
/// (again after <see cref="FeedNewsConstants.TickerMapRetry"/> when it failed). A failing feed
/// goes to the message log once per distinct error and is retried at its interval. Registered as
/// a singleton.
/// </summary>
public class NewsFeedPoller(
	IFeedClient feedClient,
	ISecApiClient secApiClient,
	INewsArchiveManager archiveManager,
	IMessageLogManager messageLogManager,
	ISettingsManager settingsManager,
	TimeProvider timeProvider
) : INewsFeedPoller
{
	private const string LogSource = "News feeds";
	private readonly Lock _lock = new();
	private readonly Dictionary<string, FeedWatch> _watches = [];

	// Symbol feed URL → when it was fetched for history and whether its page was full
	private readonly Dictionary<string, (DateTimeOffset At, bool HasMore)> _fetched = [];
	private readonly Dictionary<string, string> _errors = [];
	private readonly SemaphoreSlim _tickerGate = new(1, 1);
	private IReadOnlyDictionary<string, int>? _tickerCiks;
	private IReadOnlyDictionary<int, IReadOnlyList<string>>? _cikTickers;
	private DateTimeOffset _tickerMapFailedAt = DateTimeOffset.MinValue;

	public async IAsyncEnumerable<NewsHeadlineDto> WatchAsync(
		string providerCode,
		string? symbol,
		[EnumeratorCancellation] CancellationToken cancellationToken = default
	)
	{
		if (NewsFeedHelpers.FindFeed(providerCode) is not { } feed)
			yield break;
		IReadOnlyList<string> urls;
		if (symbol == null)
			urls = feed.BroadUrls;
		else
		{
			var cik =
				feed.Kind == NewsFeedKind.Sec ? await GetCikAsync(symbol, cancellationToken) : null;
			urls = NewsFeedHelpers.BuildSymbolUrl(feed, symbol, cik, null) is { } url ? [url] : [];
		}
		if (urls.Count == 0)
			yield break;

		var channel = Channel.CreateUnbounded<NewsHeadlineDto>(
			new UnboundedChannelOptions { SingleReader = true }
		);
		var watched = urls.Select(x => Subscribe(feed, x, symbol, channel)).ToList();
		try
		{
			await foreach (var headline in channel.Reader.ReadAllAsync(cancellationToken))
				yield return headline;
		}
		finally
		{
			foreach (var watch in watched)
				Unsubscribe(watch, channel);
		}
	}

	public async Task<NewsHeadlinesPageDto> FetchAsync(
		string providerCode,
		string symbol,
		DateTimeOffset? before,
		CancellationToken cancellationToken = default
	)
	{
		if (NewsFeedHelpers.FindFeed(providerCode) is not { } feed || feed.SymbolUrl == null)
			return new NewsHeadlinesPageDto();
		var cik =
			feed.Kind == NewsFeedKind.Sec ? await GetCikAsync(symbol, cancellationToken) : null;
		if (NewsFeedHelpers.BuildSymbolUrl(feed, symbol, cik, before) is not { } url)
			return new NewsHeadlinesPageDto();
		lock (_lock)
		{
			var now = timeProvider.GetUtcNow();
			// Asked again soon (another widget): the archive has them; older ones still exist
			if (
				_fetched.TryGetValue(url, out var fetched)
				&& now - fetched.At < FeedNewsConstants.SymbolFetchTtl
			)
				return new NewsHeadlinesPageDto { HasMore = fetched.HasMore };
			_fetched[url] = (now, false);
		}
		try
		{
			var (headlines, _, items) = await PollAsync(feed, url, symbol, null, cancellationToken);
			archiveManager.Add(headlines);
			var hasMore = feed.Kind == NewsFeedKind.Sec && items >= FeedNewsConstants.SecPageSize;
			lock (_lock)
				if (_fetched.TryGetValue(url, out var fetched))
					_fetched[url] = (fetched.At, hasMore);
			return new NewsHeadlinesPageDto { Headlines = headlines, HasMore = hasMore };
		}
		catch
		{
			// Not fetched after all: the next ask tries again
			lock (_lock)
				_fetched.Remove(url);
			throw;
		}
	}

	private FeedWatch Subscribe(
		NewsFeedDefinitionDto feed,
		string url,
		string? symbol,
		Channel<NewsHeadlineDto> channel
	)
	{
		lock (_lock)
		{
			if (!_watches.TryGetValue(url, out var watch))
			{
				watch = new FeedWatch(feed, url, symbol);
				_watches[url] = watch;
				watch.Loop = Task.Run(() => RunAsync(watch));
			}
			watch.Subscribers.Add(channel);
			return watch;
		}
	}

	private void Unsubscribe(FeedWatch watch, Channel<NewsHeadlineDto> channel)
	{
		lock (_lock)
		{
			watch.Subscribers.Remove(channel);
			if (watch.Subscribers.Count > 0)
				return;
			_watches.Remove(watch.Url);
			watch.Stop.Cancel();
		}
	}

	private async Task RunAsync(FeedWatch watch)
	{
		var token = watch.Stop.Token;
		while (!token.IsCancellationRequested)
		{
			try
			{
				var (headlines, conditions, _) = await PollAsync(
					watch.Feed,
					watch.Url,
					watch.Symbol,
					watch.Conditions,
					token
				);
				watch.Conditions = conditions;
				var added = archiveManager.Add(headlines);
				lock (_lock)
					foreach (var subscriber in watch.Subscribers)
					foreach (var headline in added)
						subscriber.Writer.TryWrite(headline);
				ReportRecovered(watch);
			}
			catch (OperationCanceledException) when (token.IsCancellationRequested)
			{
				break;
			}
			catch (Exception ex)
			{
				ReportError(watch, ex);
			}
			try
			{
				await Task.Delay(watch.Feed.PollInterval, timeProvider, token);
			}
			catch (OperationCanceledException)
			{
				break;
			}
		}
		watch.Stop.Dispose();
	}

	// One request: the feed's items as headlines (none when it didn't change), without routine SEC
	// forms and items older than the archive keeps; with how many items the feed listed
	private async Task<(
		IReadOnlyList<NewsHeadlineDto> Headlines,
		FeedConditionsDto? Conditions,
		int Items
	)> PollAsync(
		NewsFeedDefinitionDto feed,
		string url,
		string? symbol,
		FeedConditionsDto? conditions,
		CancellationToken cancellationToken
	)
	{
		var response = await feedClient.GetAsync(url, conditions, cancellationToken);
		if (response.NotModified)
			return ([], conditions, 0);
		var cikTickers =
			feed.Kind == NewsFeedKind.Sec ? await GetCikTickersAsync(cancellationToken) : null;
		var now = timeProvider.GetUtcNow();
		var retention = settingsManager.GetAppSettings().NewsRetentionDays;
		return (
			[
				.. response
					.Items.Select(x => NewsFeedHelpers.ToHeadline(feed, x, symbol, cikTickers, now))
					.OfType<NewsHeadlineDto>()
					.Where(x => NewsFeedHelpers.IsWithinRetention(x.Time, now, retention)),
			],
			response.Conditions,
			response.Items.Count
		);
	}

	private async Task<int?> GetCikAsync(string symbol, CancellationToken cancellationToken)
	{
		await GetCikTickersAsync(cancellationToken);
		return _tickerCiks?.TryGetValue(NewsFeedHelpers.ToFeedSymbol(symbol), out var cik) == true
			? cik
			: null;
	}

	// The SEC ticker file once per run; null (filings untagged) while it can't be read
	private async Task<IReadOnlyDictionary<int, IReadOnlyList<string>>?> GetCikTickersAsync(
		CancellationToken cancellationToken
	)
	{
		if (_cikTickers != null)
			return _cikTickers;
		await _tickerGate.WaitAsync(cancellationToken);
		try
		{
			if (
				_cikTickers != null
				|| timeProvider.GetUtcNow() - _tickerMapFailedAt < FeedNewsConstants.TickerMapRetry
			)
				return _cikTickers;
			try
			{
				var tickerCiks = await secApiClient.GetTickerCiksAsync(cancellationToken);
				_tickerCiks = tickerCiks;
				_cikTickers = NewsFeedHelpers.InvertTickerCiks(tickerCiks);
			}
			catch (Exception ex) when (ex is not OperationCanceledException)
			{
				_tickerMapFailedAt = timeProvider.GetUtcNow();
				messageLogManager.Add(
					MessageSeverity.Warning,
					LogSource,
					$"SEC tickers couldn't be read; filings stay untagged for now: {ex.Message}"
				);
			}
			return _cikTickers;
		}
		finally
		{
			_tickerGate.Release();
		}
	}

	private void ReportError(FeedWatch watch, Exception ex)
	{
		var message = ex is FeedServiceException ? ex.Message : $"{watch.Feed.Code}: {ex.Message}";
		lock (_lock)
		{
			if (_errors.TryGetValue(watch.Url, out var last) && last == message)
				return;
			_errors[watch.Url] = message;
		}
		messageLogManager.Add(MessageSeverity.Warning, LogSource, $"{watch.Feed.Name}: {message}");
	}

	private void ReportRecovered(FeedWatch watch)
	{
		lock (_lock)
			if (!_errors.Remove(watch.Url))
				return;
		messageLogManager.Add(
			MessageSeverity.Info,
			LogSource,
			$"{watch.Feed.Name}: the feed answers again."
		);
	}

	private sealed class FeedWatch(NewsFeedDefinitionDto feed, string url, string? symbol)
	{
		public NewsFeedDefinitionDto Feed { get; } = feed;
		public string Url { get; } = url;
		public string? Symbol { get; } = symbol;
		public CancellationTokenSource Stop { get; } = new();
		public List<Channel<NewsHeadlineDto>> Subscribers { get; } = [];
		public FeedConditionsDto? Conditions { get; set; }
		public Task? Loop { get; set; }
	}
}
