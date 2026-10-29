using LSCore.Mapper.Domain;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.News;
using LT.Contracts.Entities;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;
using LT.Contracts.Interfaces.IManagers;
using LT.Contracts.Interfaces.IRepositories;

namespace LT.Domain.Managers;

/// <summary>
/// The local news archive over <see cref="INewsArchiveRepository"/> (one file per UTC day). Keeps
/// in memory the keys of every day it touched (each headline is stored once), the headlines of
/// the newest <see cref="NewsConstants.ArchiveCachedDays"/> days, and per day read the symbols and
/// providers in it, so a symbol's history skips days without it. Disk errors are swallowed: news
/// keeps working without the archive. Registered as a singleton.
/// </summary>
public class NewsArchiveManager(
	INewsArchiveRepository repository,
	ISettingsManager settingsManager,
	TimeProvider timeProvider
) : INewsArchiveManager
{
	private readonly Lock _lock = new();
	private readonly Dictionary<DateOnly, HashSet<string>> _keys = [];
	private readonly Dictionary<DateOnly, List<NewsHeadlineDto>> _days = [];
	private readonly Dictionary<
		DateOnly,
		(HashSet<string> Symbols, HashSet<string> Providers)
	> _summaries = [];

	public event Action<IReadOnlyList<NewsHeadlineDto>>? HeadlinesAdded;

	public IReadOnlyList<NewsHeadlineDto> Add(IEnumerable<NewsHeadlineDto> headlines)
	{
		var added = new List<NewsHeadlineDto>();
		var byDay = new Dictionary<DateOnly, List<NewsHeadlineEntity>>();
		lock (_lock)
		{
			foreach (var headline in headlines)
			{
				var day = DateOnly.FromDateTime(headline.Time.UtcDateTime);
				if (!GetKeys(day).Add(NewsHelpers.GetKey(headline)))
					continue;
				added.Add(headline);
				if (!byDay.TryGetValue(day, out var entities))
					byDay[day] = entities = [];
				entities.Add(headline.ToMapped<NewsHeadlineDto, NewsHeadlineEntity>());
				if (_days.TryGetValue(day, out var cached))
					cached.Add(headline);
				if (_summaries.TryGetValue(day, out var summary))
					NewsHelpers.AddToIndex(summary.Symbols, summary.Providers, headline);
			}
			try
			{
				foreach (var (day, entities) in byDay)
					repository.AppendHeadlines(day, entities);
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				// Shown anyway; only the archive misses them
			}
		}
		if (added.Count > 0)
			try
			{
				HeadlinesAdded?.Invoke(added);
			}
			catch (Exception)
			{
				// A listener's failure never stops the news
			}
		return added;
	}

	public NewsHeadlinesPageDto GetHeadlines(NewsArchiveQueryDto query)
	{
		var symbol = query.Symbol is { Length: > 0 } s ? NewsFeedHelpers.ToFeedSymbol(s) : null;
		var found = new List<NewsHeadlineDto>();
		lock (_lock)
		{
			IReadOnlyList<DateOnly> days;
			try
			{
				days = repository.GetDays();
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				return new NewsHeadlinesPageDto();
			}
			var last = query.Before is { } before
				? DateOnly.FromDateTime(before.UtcDateTime)
				: DateOnly.MaxValue;
			foreach (var day in days.Where(x => x <= last).OrderDescending())
			{
				if (
					_summaries.TryGetValue(day, out var summary)
					&& (
						(symbol != null && !summary.Symbols.Contains(symbol))
						|| (
							query.ProviderKeys != null
							&& !summary.Providers.Overlaps(query.ProviderKeys)
						)
					)
				)
					continue;
				found.AddRange(
					GetDay(day)
						.Where(x =>
							(query.Before == null || x.Time < query.Before)
							&& (
								query.ProviderKeys == null
								|| query.ProviderKeys.Contains(
									NewsHelpers.GetProviderKey(x.SourceId, x.ProviderCode)
								)
							)
							&& (symbol == null || NewsFeedHelpers.IsAbout(x, symbol))
							&& NewsHelpers.MatchesSearch(x, query.Text)
							// Routine fund filings archived before they were left out
							&& !NewsFeedHelpers.IsExcludedSecHeadline(x)
						)
						.OrderByDescending(x => x.Time)
				);
				// Days are disjoint: once past the limit, older days can only come after
				if (found.Count > query.Limit)
					break;
			}
		}
		return new NewsHeadlinesPageDto
		{
			Headlines = found.Take(query.Limit).ToList(),
			HasMore = found.Count > query.Limit,
		};
	}

	public NewsArticleDto? GetArticle(NewsHeadlineDto headline)
	{
		var key = NewsHelpers.GetKey(headline);
		try
		{
			return
				repository
					.GetArticles(DateOnly.FromDateTime(headline.Time.UtcDateTime))
					.LastOrDefault(x => x.Key == key)
					is { } article
				? new NewsArticleDto
				{
					Kind = article.Kind,
					Body = article.Body,
					Url = article.Url,
				}
				: null;
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			return null;
		}
	}

	public void SaveArticle(NewsHeadlineDto headline, NewsArticleDto article)
	{
		if (article.Kind == NewsArticleKind.Binary)
			return;
		try
		{
			repository.AppendArticle(
				DateOnly.FromDateTime(headline.Time.UtcDateTime),
				new NewsArticleEntity
				{
					Key = NewsHelpers.GetKey(headline),
					Kind = article.Kind,
					Body = article.Body,
					Url = article.Url,
				}
			);
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
	}

	public long GetSizeBytes()
	{
		try
		{
			return repository.GetSizeBytes();
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			return 0;
		}
	}

	public void Clear()
	{
		lock (_lock)
		{
			_keys.Clear();
			_days.Clear();
			_summaries.Clear();
			try
			{
				repository.Clear();
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
		}
	}

	public void ApplyRetention()
	{
		var days = settingsManager.GetAppSettings().NewsRetentionDays;
		if (days <= 0)
			return;
		var oldest = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime).AddDays(-days);
		lock (_lock)
		{
			try
			{
				foreach (var day in repository.GetDays().Where(x => x < oldest))
				{
					repository.DeleteDay(day);
					_keys.Remove(day);
					_days.Remove(day);
					_summaries.Remove(day);
				}
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
		}
	}

	// Under _lock: the day's keys, read from its file the first time
	private HashSet<string> GetKeys(DateOnly day)
	{
		if (!_keys.TryGetValue(day, out var keys))
			_keys[day] = keys = [.. GetDay(day).Select(NewsHelpers.GetKey)];
		return keys;
	}

	// Under _lock: the day's headlines, kept in memory for the newest days only
	private IReadOnlyList<NewsHeadlineDto> GetDay(DateOnly day)
	{
		if (_days.TryGetValue(day, out var cached))
			return cached;
		List<NewsHeadlineDto> headlines;
		try
		{
			headlines =
			[
				.. repository
					.GetHeadlines(day)
					.Select(x => x.ToMapped<NewsHeadlineEntity, NewsHeadlineDto>()),
			];
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			headlines = [];
		}
		(HashSet<string> Symbols, HashSet<string> Providers) summary = ([], []);
		foreach (var headline in headlines)
			NewsHelpers.AddToIndex(summary.Symbols, summary.Providers, headline);
		_summaries[day] = summary;

		var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
		if (day > today.AddDays(-NewsConstants.ArchiveCachedDays))
			_days[day] = headlines;
		return headlines;
	}
}
