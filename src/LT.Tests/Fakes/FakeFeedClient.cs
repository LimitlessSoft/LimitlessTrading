using LT.Contracts.Dtos.Feeds;
using LT.Contracts.Exceptions;
using LT.Contracts.Interfaces.IApiClients;
using LT.Feeds;

namespace LT.Tests.Fakes;

/// <summary>Answers feed URLs with canned XML (parsed like the real client) or text; records every request.</summary>
public class FakeFeedClient : IFeedClient
{
	private readonly Lock _lock = new();

	/// <summary>Feed XML by URL; a URL not here fails like a 404.</summary>
	public Dictionary<string, string> Feeds { get; } = [];

	/// <summary>Pages (HTML) by URL for <see cref="GetTextAsync"/>.</summary>
	public Dictionary<string, string> Texts { get; } = [];

	/// <summary>URLs whose feed answers 304 when asked with conditions.</summary>
	public HashSet<string> Unchanged { get; } = [];

	public List<(string Url, FeedConditionsDto? Conditions)> Requests { get; } = [];

	public int RequestCount(string url)
	{
		lock (_lock)
			return Requests.Count(x => x.Url == url);
	}

	public Task<FeedResponseDto> GetAsync(
		string url,
		FeedConditionsDto? conditions = null,
		CancellationToken cancellationToken = default
	)
	{
		lock (_lock)
		{
			Requests.Add((url, conditions));
			if (conditions?.ETag != null && Unchanged.Contains(url))
				return Task.FromResult(
					new FeedResponseDto { NotModified = true, Conditions = conditions }
				);
			if (!Feeds.TryGetValue(url, out var xml))
				return Task.FromException<FeedResponseDto>(
					new FeedServiceException(
						$"Couldn't read the {new Uri(url).Host} feed: HTTP 404"
					)
				);
			var response = FeedParser.Parse(xml);
			response.Conditions = new FeedConditionsDto { ETag = $"\"{xml.GetHashCode()}\"" };
			return Task.FromResult(response);
		}
	}

	public Task<string> GetTextAsync(string url, CancellationToken cancellationToken = default)
	{
		lock (_lock)
		{
			Requests.Add((url, null));
			return Texts.TryGetValue(url, out var text)
				? Task.FromResult(text)
				: Task.FromException<string>(new FeedServiceException($"HTTP 404 {url}"));
		}
	}

	/// <summary>An RSS feed of the items given (guid, title, minutes after 12:00 UTC on 2026-10-29, description).</summary>
	public static string Rss(
		params (string Id, string Title, int Minute, string? Description)[] items
	) =>
		"<?xml version=\"1.0\" encoding=\"UTF-8\"?><rss version=\"2.0\"><channel><title>Test</title>"
		+ string.Concat(
			items.Select(x =>
				$"<item><title>{x.Title}</title><link>https://example.com/{x.Id}</link><guid>{x.Id}</guid>"
				+ $"<pubDate>Thu, 29 Oct 2026 12:{x.Minute:00}:00 GMT</pubDate>"
				+ (x.Description == null ? "" : $"<description>{x.Description}</description>")
				+ "</item>"
			)
		)
		+ "</channel></rss>";
}
