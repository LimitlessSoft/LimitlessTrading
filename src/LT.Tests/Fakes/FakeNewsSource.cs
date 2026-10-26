using System.Runtime.CompilerServices;
using LT.Contracts.Dtos.News;
using LT.Contracts.Enums;
using LT.Contracts.Interfaces.INewsSources;
using LT.Contracts.Requests.News;

namespace LT.Tests.Fakes;

/// <summary>A news source answering from lists; records what it was asked.</summary>
public class FakeNewsSource(string id) : INewsSource
{
	public string Id => id;
	public string Name => id;
	public NewsSourceCapabilities Capabilities { get; set; } =
		NewsSourceCapabilities.Live
		| NewsSourceCapabilities.History
		| NewsSourceCapabilities.Articles
		| NewsSourceCapabilities.BySymbol
		| NewsSourceCapabilities.BroadTape;
	public bool IsAvailable { get; set; } = true;

	public List<NewsProviderDto> Providers { get; set; } = [];
	public List<NewsHeadlineDto> History { get; set; } = [];
	public bool HasMore { get; set; }
	public List<NewsHeadlineDto> Live { get; set; } = [];
	public HashSet<string> BroadTapes { get; set; } = [];
	public Exception? Error { get; set; }
	public List<GetNewsHeadlinesRequest> HeadlineRequests { get; } = [];
	public List<SubscribeNewsRequest> Subscriptions { get; } = [];
	public int ArticleCalls { get; private set; }

	public NewsProviderDto Provider(string code) =>
		new()
		{
			SourceId = id,
			Code = code,
			Name = code,
		};

	public NewsHeadlineDto Headline(string provider, string articleId, int minute) =>
		new()
		{
			SourceId = id,
			ProviderCode = provider,
			ArticleId = articleId,
			Time = new DateTimeOffset(2026, 10, 26, 12, minute, 0, TimeSpan.Zero),
			Headline = $"{provider} {articleId}",
		};

	public Task<IReadOnlyList<NewsProviderDto>> GetProvidersAsync(
		CancellationToken cancellationToken = default
	) =>
		Error != null
			? Task.FromException<IReadOnlyList<NewsProviderDto>>(Error)
			: Task.FromResult<IReadOnlyList<NewsProviderDto>>(Providers);

	public Task<NewsHeadlinesPageDto> GetHeadlinesAsync(
		GetNewsHeadlinesRequest request,
		CancellationToken cancellationToken = default
	)
	{
		HeadlineRequests.Add(request);
		return Error != null
			? Task.FromException<NewsHeadlinesPageDto>(Error)
			: Task.FromResult(new NewsHeadlinesPageDto { Headlines = History, HasMore = HasMore });
	}

	public async IAsyncEnumerable<NewsHeadlineDto> SubscribeHeadlinesAsync(
		SubscribeNewsRequest request,
		[EnumeratorCancellation] CancellationToken cancellationToken = default
	)
	{
		Subscriptions.Add(request);
		foreach (var headline in Live)
		{
			await Task.Yield();
			yield return headline;
		}
	}

	public Task<bool> HasBroadTapeAsync(
		string providerCode,
		CancellationToken cancellationToken = default
	) => Task.FromResult(BroadTapes.Contains(providerCode));

	public Task<NewsArticleDto> GetArticleAsync(
		NewsHeadlineDto headline,
		CancellationToken cancellationToken = default
	)
	{
		ArticleCalls++;
		return Task.FromResult(new NewsArticleDto { Body = $"{id} {headline.ArticleId}" });
	}
}
