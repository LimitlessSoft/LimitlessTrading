using System.Net;
using LT.Sec;
using LT.Tests.Fakes;
using Microsoft.Extensions.Time.Testing;

namespace LT.Tests.Sec;

public class SecApiClientTests
{
	private readonly FakeTimeProvider _time = new(
		new DateTimeOffset(2026, 10, 26, 14, 0, 0, TimeSpan.Zero)
	);
	private readonly FakeHttpHandler _http;
	private readonly SecApiClient _client;

	public SecApiClientTests()
	{
		_http = new FakeHttpHandler(_time);
		_client = new SecApiClient(SecApiClient.CreateHttpClient(_http), _time);
	}

	// Moves the fake clock until the call finishes (the request spacing waits on it)
	private async Task<T> RunAsync<T>(Task<T> task)
	{
		for (var i = 0; i < 1000 && !task.IsCompleted; i++)
		{
			await Task.Delay(1, TestContext.Current.CancellationToken);
			_time.Advance(TimeSpan.FromMilliseconds(25));
		}
		return await task;
	}

	[Fact]
	public async Task Asks_for_the_padded_cik_with_a_contact_user_agent_and_404_is_nothing()
	{
		_http.Respond("""{"units":{"shares":[]}}""").Respond("", HttpStatusCode.NotFound);

		var found = await RunAsync(
			_client.GetSharesOutstandingFactsAsync(320193, TestContext.Current.CancellationToken)
		);
		var missing = await RunAsync(
			_client.GetCompanyShareFactsAsync(884394, TestContext.Current.CancellationToken)
		);

		Assert.NotNull(found);
		Assert.Null(missing);
		Assert.EndsWith(
			"/CIK0000320193/dei/EntityCommonStockSharesOutstanding.json",
			_http.Requests[0].Uri.AbsolutePath
		);
		Assert.EndsWith("/CIK0000884394.json", _http.Requests[1].Uri.AbsolutePath);
		Assert.Matches(@"^LimitlessTrading/\S+ \S+@\S+$", _http.Requests[0].UserAgent);
	}

	[Fact]
	public async Task Requests_are_spaced_within_the_sec_rate_limit()
	{
		_http.Respond("""{"units":{"shares":[]}}""");

		var first = _client.GetSharesOutstandingFactsAsync(
			1,
			TestContext.Current.CancellationToken
		);
		var second = _client.GetSharesOutstandingFactsAsync(
			2,
			TestContext.Current.CancellationToken
		);
		await RunAsync(Task.WhenAll(first, second).ContinueWith(_ => 0));

		Assert.Equal(2, _http.Requests.Count);
		Assert.True(_http.Requests[1].At - _http.Requests[0].At >= TimeSpan.FromMilliseconds(125));
	}

	[Fact]
	public async Task Server_errors_throw()
	{
		_http.Respond("", HttpStatusCode.Forbidden);

		await Assert.ThrowsAsync<HttpRequestException>(() =>
			RunAsync(_client.GetTickerCiksAsync(TestContext.Current.CancellationToken))
		);
	}
}
