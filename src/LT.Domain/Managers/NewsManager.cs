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
/// Aggregates every registered <see cref="INewsSource"/> and the local archive
/// (<see cref="INewsArchiveManager"/>): each source gets only its own providers, and only those
/// whose capabilities fit the request; sources that are unavailable or can't serve a request are
/// skipped, and one failing source doesn't hide the others' headlines (only when all fail and the
/// archive has nothing does the error reach the caller). Everything passed on is archived; the
/// same story from two providers is shown once (<see cref="NewsConstants.StoryWindow"/>). Keeps
/// the last <see cref="NewsConstants.ArticleCacheSize"/> articles for
/// <see cref="NewsConstants.ArticleCacheTtl"/>, and every article read in the archive.
/// Registered as a singleton.
/// </summary>
public class NewsManager(
	IEnumerable<INewsSource> sources,
	INewsArchiveManager archiveManager,
	TimeProvider timeProvider
) : INewsManager
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
			return [];

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

		var needed =
			request.Contract == null
				? NewsSourceCapabilities.BroadTape
				: NewsSourceCapabilities.BySymbol;
		var providers = request
			.Providers.Where(x => x.Capabilities.HasFlag(NewsSourceCapabilities.History | needed))
			.ToList();
		var serving = _sources
			.Where(x =>
				x.IsAvailable
				&& x.Capabilities.HasFlag(NewsSourceCapabilities.History | needed)
				&& providers.Any(p => p.SourceId == x.Id)
			)
			.ToList();
		if (providers.Count == 0)
			throw new LSCoreBadRequestException(NewsValidationCodes.NVC_004.GetValidationMessage());

		var asked = new GetNewsHeadlinesRequest
		{
			Contract = request.Contract,
			Providers = providers,
			Before = request.Before,
			Limit = request.Limit,
		};
		var archived = Task.Run(
			() =>
				archiveManager.GetHeadlines(
					new NewsArchiveQueryDto
					{
						ProviderKeys = providers.Select(NewsHelpers.GetProviderKey).ToHashSet(),
						// IB tags a contract's headlines with its display name (ESZ6 for a future)
						Symbol = request.Contract is { } contract
							? ContractHelpers.GetDisplayName(contract) is { Length: > 0 } name
								? name
								: contract.Symbol
							: null,
						Before = request.Before,
						Limit = request.Limit,
					}
				),
			cancellationToken
		);
		var results = await Task.WhenAll(
			serving.Select(async source =>
			{
				try
				{
					return (
						Page: await source.GetHeadlinesAsync(asked, cancellationToken),
						Error: (Exception?)null
					);
				}
				catch (Exception ex) when (ex is not OperationCanceledException)
				{
					return (Page: new NewsHeadlinesPageDto(), Error: ex);
				}
			})
		);
		var archive = await archived;
		if (results.Length > 0 && results.All(x => x.Error != null) && archive.Headlines.Count == 0)
			throw results[0].Error!;

		var pages = results.Where(x => x.Error == null).Select(x => x.Page).ToList();
		var fetched = pages.SelectMany(x => x.Headlines).ToList();
		archiveManager.Add(fetched);
		var all = NewsHelpers.MergeStories(
			fetched.Concat(archive.Headlines),
			int.MaxValue,
			NewsConstants.StoryWindow
		);
		return new NewsHeadlinesPageDto
		{
			Headlines = all.Take(request.Limit).ToList(),
			HasMore = all.Count > request.Limit || archive.HasMore || pages.Any(x => x.HasMore),
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
		var providers = request.Providers.Where(x => x.Capabilities.HasFlag(needed)).ToList();
		var asked = new SubscribeNewsRequest { Contract = request.Contract, Providers = providers };
		var streams = _sources
			.Where(x =>
				x.IsAvailable
				&& x.Capabilities.HasFlag(needed)
				&& providers.Any(p => p.SourceId == x.Id)
			)
			.Select(x => x.SubscribeHeadlinesAsync(asked, cancellationToken))
			.ToList();
		if (streams.Count == 0)
			throw new LSCoreBadRequestException(NewsValidationCodes.NVC_004.GetValidationMessage());

		// IB replays recent headlines on subscribe; a headline seen once isn't passed on again, nor
		// the same story from another provider
		var seen = new HashSet<string>();
		var stories = new Dictionary<string, List<(DateTimeOffset, string)>>();
		await foreach (
			var headline in AsyncEnumerableHelpers.MergeAsync(streams, cancellationToken)
		)
		{
			if (!seen.Add(NewsHelpers.GetKey(headline)))
				continue;
			archiveManager.Add([headline]);
			if (!NewsHelpers.IsRepeatedStory(stories, headline, NewsConstants.StoryWindow))
				yield return headline;
		}
	}

	public async Task<IReadOnlyList<NewsProviderDto>> GetBroadTapeProvidersAsync(
		IEnumerable<NewsProviderDto> providers,
		CancellationToken cancellationToken = default
	)
	{
		var result = new List<NewsProviderDto>();
		foreach (var provider in providers)
			if (
				provider.Capabilities.HasFlag(NewsSourceCapabilities.BroadTape)
				&& _sources.FirstOrDefault(x => x.Id == provider.SourceId) is { } source
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
		var key = NewsHelpers.GetKey(headline);
		lock (_articlesLock)
			if (
				_articles.TryGetValue(key, out var cached)
				&& timeProvider.GetUtcNow() - cached.FetchedAt < NewsConstants.ArticleCacheTtl
			)
				return cached.Article;

		var article = await Task.Run(() => archiveManager.GetArticle(headline), cancellationToken);
		if (article == null)
		{
			var source = _sources.FirstOrDefault(x => x.Id == headline.SourceId);
			if (
				source == null
				|| !source.IsAvailable
				|| !source.Capabilities.HasFlag(NewsSourceCapabilities.Articles)
			)
				throw new LSCoreBadRequestException(
					NewsValidationCodes.NVC_005.GetValidationMessage()
				);
			article = await source.GetArticleAsync(headline, cancellationToken);
			article.Url ??= headline.Url;
			archiveManager.SaveArticle(headline, article);
		}

		lock (_articlesLock)
		{
			_articles[key] = (article, timeProvider.GetUtcNow());
			while (_articles.Count > NewsConstants.ArticleCacheSize)
				_articles.Remove(_articles.MinBy(x => x.Value.FetchedAt).Key);
		}
		return article;
	}

	public Task<NewsHeadlinesPageDto> SearchArchiveAsync(
		SearchNewsRequest request,
		CancellationToken cancellationToken = default
	)
	{
		request.Validate();
		return Task.Run(
			() =>
				archiveManager.GetHeadlines(
					new NewsArchiveQueryDto
					{
						ProviderKeys =
							request.Providers.Count == 0
								? null
								: request.Providers.Select(NewsHelpers.GetProviderKey).ToHashSet(),
						Text = request.Query.Trim(),
						Before = request.Before,
						Limit = request.Limit,
					}
				),
			cancellationToken
		);
	}

	public long GetArchiveSizeBytes() => archiveManager.GetSizeBytes();

	public void ClearArchive()
	{
		archiveManager.Clear();
		lock (_articlesLock)
			_articles.Clear();
	}
}
