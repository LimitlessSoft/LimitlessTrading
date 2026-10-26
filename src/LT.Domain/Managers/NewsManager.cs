using System.Runtime.CompilerServices;
using LSCore.Exceptions;
using LSCore.Validation.Contracts;
using LSCore.Validation.Domain;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.News;
using LT.Contracts.Enums;
using LT.Contracts.Enums.ValidationCodes;
using LT.Contracts.Helpers;
using LT.Contracts.Interfaces.IManagers;
using LT.Contracts.Interfaces.INewsSources;
using LT.Contracts.Requests.News;

namespace LT.Domain.Managers;

/// <summary>
/// Aggregates every registered <see cref="INewsSource"/>: each gets only its own providers, sources
/// that are unavailable or can't serve a request are skipped, and one failing source doesn't hide
/// the others' headlines (only when all fail does the error reach the caller). Keeps the last
/// <see cref="NewsConstants.ArticleCacheSize"/> articles for <see cref="NewsConstants.ArticleCacheTtl"/>.
/// Registered as a singleton.
/// </summary>
public class NewsManager(IEnumerable<INewsSource> sources, TimeProvider timeProvider) : INewsManager
{
	private readonly IReadOnlyList<INewsSource> _sources = [.. sources];
	private readonly Lock _articlesLock = new();
	private readonly Dictionary<
		string,
		(NewsArticleDto Article, DateTimeOffset FetchedAt)
	> _articles = [];

	public async Task<IReadOnlyList<NewsProviderDto>> GetProvidersAsync(
		CancellationToken cancellationToken = default
	)
	{
		var available = _sources.Where(x => x.IsAvailable).ToList();
		if (available.Count == 0)
			throw new LSCoreBadRequestException(NewsValidationCodes.NVC_004.GetValidationMessage());

		var results = await Task.WhenAll(
			available.Select(async source =>
			{
				try
				{
					return (
						Providers: await source.GetProvidersAsync(cancellationToken),
						Error: (Exception?)null
					);
				}
				catch (Exception ex) when (ex is not OperationCanceledException)
				{
					return (Providers: (IReadOnlyList<NewsProviderDto>)[], Error: ex);
				}
			})
		);
		if (results.All(x => x.Error != null))
			throw results[0].Error!;
		return [.. results.SelectMany(x => x.Providers)];
	}

	public async Task<NewsHeadlinesPageDto> GetHeadlinesAsync(
		GetNewsHeadlinesRequest request,
		CancellationToken cancellationToken = default
	)
	{
		request.Validate();

		var serving = _sources
			.Where(x =>
				x.IsAvailable
				&& x.Capabilities.HasFlag(
					NewsSourceCapabilities.History | NewsSourceCapabilities.BySymbol
				)
				&& request.Providers.Any(p => p.SourceId == x.Id)
			)
			.ToList();
		if (serving.Count == 0)
			throw new LSCoreBadRequestException(NewsValidationCodes.NVC_004.GetValidationMessage());

		var results = await Task.WhenAll(
			serving.Select(async source =>
			{
				try
				{
					return (
						Page: await source.GetHeadlinesAsync(request, cancellationToken),
						Error: (Exception?)null
					);
				}
				catch (Exception ex) when (ex is not OperationCanceledException)
				{
					return (Page: new NewsHeadlinesPageDto(), Error: ex);
				}
			})
		);
		if (results.All(x => x.Error != null))
			throw results[0].Error!;

		var pages = results.Where(x => x.Error == null).Select(x => x.Page).ToList();
		var all = NewsHelpers.Merge(pages.SelectMany(x => x.Headlines), [], int.MaxValue);
		return new NewsHeadlinesPageDto
		{
			Headlines = all.Take(request.Limit).ToList(),
			HasMore = all.Count > request.Limit || pages.Any(x => x.HasMore),
		};
	}

	public async IAsyncEnumerable<NewsHeadlineDto> SubscribeHeadlinesAsync(
		SubscribeNewsRequest request,
		[EnumeratorCancellation] CancellationToken cancellationToken = default
	)
	{
		request.Validate();

		var needed =
			NewsSourceCapabilities.Live
			| (
				request.Contract == null
					? NewsSourceCapabilities.BroadTape
					: NewsSourceCapabilities.BySymbol
			);
		var streams = _sources
			.Where(x =>
				x.IsAvailable
				&& x.Capabilities.HasFlag(needed)
				&& request.Providers.Any(p => p.SourceId == x.Id)
			)
			.Select(x => x.SubscribeHeadlinesAsync(request, cancellationToken))
			.ToList();
		if (streams.Count == 0)
			throw new LSCoreBadRequestException(NewsValidationCodes.NVC_004.GetValidationMessage());

		// IB replays recent headlines on subscribe; a headline seen once isn't passed on again
		var seen = new HashSet<string>();
		await foreach (
			var headline in AsyncEnumerableHelpers.MergeAsync(streams, cancellationToken)
		)
			if (seen.Add(NewsHelpers.GetKey(headline)))
				yield return headline;
	}

	public async Task<IReadOnlyList<NewsProviderDto>> GetBroadTapeProvidersAsync(
		IEnumerable<NewsProviderDto> providers,
		CancellationToken cancellationToken = default
	)
	{
		var result = new List<NewsProviderDto>();
		foreach (var provider in providers)
			if (
				_sources.FirstOrDefault(x => x.Id == provider.SourceId) is { } source
				&& source.IsAvailable
				&& source.Capabilities.HasFlag(
					NewsSourceCapabilities.Live | NewsSourceCapabilities.BroadTape
				)
				&& await source.HasBroadTapeAsync(provider.Code, cancellationToken)
			)
				result.Add(provider);
		return result;
	}

	public async Task<NewsArticleDto> GetArticleAsync(
		NewsHeadlineDto headline,
		CancellationToken cancellationToken = default
	)
	{
		var source = _sources.FirstOrDefault(x => x.Id == headline.SourceId);
		if (source == null || !source.Capabilities.HasFlag(NewsSourceCapabilities.Articles))
			throw new LSCoreBadRequestException(NewsValidationCodes.NVC_005.GetValidationMessage());

		var key = NewsHelpers.GetKey(headline);
		lock (_articlesLock)
			if (
				_articles.TryGetValue(key, out var cached)
				&& timeProvider.GetUtcNow() - cached.FetchedAt < NewsConstants.ArticleCacheTtl
			)
				return cached.Article;

		var article = await source.GetArticleAsync(headline, cancellationToken);
		lock (_articlesLock)
		{
			_articles[key] = (article, timeProvider.GetUtcNow());
			while (_articles.Count > NewsConstants.ArticleCacheSize)
				_articles.Remove(_articles.MinBy(x => x.Value.FetchedAt).Key);
		}
		return article;
	}
}
