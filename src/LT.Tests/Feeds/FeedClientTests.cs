using System.Net;
using System.Net.Http.Headers;
using LT.Contracts.Dtos.Feeds;
using LT.Contracts.Exceptions;
using LT.Feeds;
using LT.Tests.Fakes;
using Microsoft.Extensions.Time.Testing;

namespace LT.Tests.Feeds;

public class FeedClientTests
{
	private const string PrnUrl = "https://www.prnewswire.com/rss/news-releases-list.rss";
	private readonly FakeTimeProvider _time = new(
		new DateTimeOffset(2026, 10, 29, 14, 0, 0, TimeSpan.Zero)
	);
	private readonly RecordingHandler _http;
	private readonly FeedClient _client;

	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	public FeedClientTests()
	{
		LSCoreContainer.EnsureInitialized();
		_http = new RecordingHandler { Clock = _time };
		_client = new FeedClient(FeedClient.CreateHttpClient(_http), _time);
	}

	// Moves the fake clock until the call finishes (the per-host spacing waits on it)
	private async Task<T> RunAsync<T>(Task<T> task)
	{
		for (var i = 0; i < 1000 && !task.IsCompleted; i++)
		{
			await Task.Delay(1, Ct);
			_time.Advance(TimeSpan.FromMilliseconds(50));
		}
		return await task;
	}

	[Fact]
	public async Task A_feed_is_read_with_the_contact_user_agent_the_sec_requires()
	{
		_http.Respond(_ => Ok(FeedFixtures.Read("prnewswire.xml"), "\"v1\""));

		var feed = await RunAsync(_client.GetAsync(PrnUrl, null, Ct));

		Assert.Equal(3, feed.Items.Count);
		Assert.False(feed.NotModified);
		Assert.Equal("\"v1\"", feed.Conditions.ETag);
		var userAgent = _http.Requests[0].Headers.UserAgent.ToString();
		Assert.StartsWith("LimitlessTrading/", userAgent);
		Assert.Contains("@", userAgent);
	}

	[Fact]
	public async Task An_unchanged_feed_costs_a_304()
	{
		_http.Respond(request =>
			request.Headers.IfNoneMatch.Any(x => x.Tag == "\"v1\"")
				? new HttpResponseMessage(HttpStatusCode.NotModified)
				: Ok(FeedFixtures.Read("prnewswire.xml"), "\"v1\"")
		);

		var first = await RunAsync(_client.GetAsync(PrnUrl, null, Ct));
		var second = await RunAsync(_client.GetAsync(PrnUrl, first.Conditions, Ct));

		Assert.True(second.NotModified);
		Assert.Empty(second.Items);
		Assert.Equal("\"v1\"", second.Conditions.ETag);
	}

	[Fact]
	public async Task Requests_to_one_host_are_spaced()
	{
		_http.Respond(_ => Ok(FeedFixtures.Read("prnewswire.xml"), null));

		await RunAsync(_client.GetAsync(PrnUrl, null, Ct));
		await RunAsync(_client.GetAsync(PrnUrl, null, Ct));

		Assert.True(_http.Times[1] - _http.Times[0] >= TimeSpan.FromMilliseconds(250));
	}

	[Fact]
	public async Task A_refused_feed_names_the_host_and_status()
	{
		_http.Respond(_ => new HttpResponseMessage(HttpStatusCode.Forbidden));

		var error = await Assert.ThrowsAsync<FeedServiceException>(() =>
			RunAsync(_client.GetAsync("https://www.globenewswire.com/RssFeed/x", null, Ct))
		);

		Assert.Contains("www.globenewswire.com", error.Message);
		Assert.Contains("403", error.Message);
	}

	[Fact]
	public async Task A_page_that_is_not_a_feed_is_an_error_not_a_crash()
	{
		_http.Respond(_ => Ok("<html><body>Access Denied</body></html>", null));

		await Assert.ThrowsAsync<FeedServiceException>(() =>
			RunAsync(_client.GetAsync(PrnUrl, null, Ct))
		);
	}

	private static HttpResponseMessage Ok(string body, string? etag)
	{
		var response = new HttpResponseMessage(HttpStatusCode.OK)
		{
			Content = new StringContent(body),
		};
		if (etag != null)
			response.Headers.ETag = EntityTagHeaderValue.Parse(etag);
		return response;
	}

	private sealed class RecordingHandler : HttpMessageHandler
	{
		private Func<HttpRequestMessage, HttpResponseMessage> _respond =
			_ => new HttpResponseMessage(HttpStatusCode.NotFound);

		public List<HttpRequestMessage> Requests { get; } = [];
		public List<DateTimeOffset> Times { get; } = [];
		public TimeProvider? Clock { get; set; }

		public void Respond(Func<HttpRequestMessage, HttpResponseMessage> respond) =>
			_respond = respond;

		protected override Task<HttpResponseMessage> SendAsync(
			HttpRequestMessage request,
			CancellationToken cancellationToken
		)
		{
			Requests.Add(request);
			Times.Add(Clock?.GetUtcNow() ?? DateTimeOffset.UtcNow);
			return Task.FromResult(_respond(request));
		}
	}
}
