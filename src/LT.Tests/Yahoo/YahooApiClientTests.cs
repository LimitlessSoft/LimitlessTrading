using System.Net;
using LT.Contracts.Exceptions;
using LT.Tests.Fakes;
using LT.Yahoo;
using Microsoft.Extensions.Time.Testing;

namespace LT.Tests.Yahoo;

public class YahooApiClientTests
{
	private static readonly DateOnly Thursday = new(2026, 10, 29);

	private readonly FakeTimeProvider _time = new(
		new DateTimeOffset(2026, 10, 29, 21, 30, 0, TimeSpan.Zero)
	);
	private readonly FakeHttpHandler _http;
	private readonly YahooApiClient _client;

	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	public YahooApiClientTests()
	{
		LSCoreContainer.EnsureInitialized();
		_http = new FakeHttpHandler(_time);
		_client = new YahooApiClient(YahooApiClient.CreateHttpClient(_http), _time);
	}

	// Moves the fake clock until the call finishes (the request spacing waits on it)
	private async Task<T> RunAsync<T>(Task<T> task)
	{
		for (var i = 0; i < 1000 && !task.IsCompleted; i++)
		{
			await Task.Delay(1, Ct);
			_time.Advance(TimeSpan.FromMilliseconds(100));
		}
		return await task;
	}

	[Fact]
	public async Task Asks_for_the_day_and_offset_as_a_browser_and_spaces_the_requests()
	{
		_http.Respond(YahooFixtures.Earnings);

		var page = await RunAsync(_client.GetEarningsPageAsync(Thursday, 0, Ct));
		await RunAsync(_client.GetEarningsPageAsync(Thursday, 100, Ct));

		Assert.Equal(7, page.Rows.Count);
		Assert.Equal(
			"https://finance.yahoo.com/calendar/earnings?day=2026-10-29&offset=0&size=100",
			_http.Requests[0].Uri.ToString()
		);
		Assert.EndsWith("offset=100&size=100", _http.Requests[1].Uri.ToString());
		Assert.StartsWith("Mozilla/5.0", _http.Requests[0].UserAgent);
		Assert.True(_http.Requests[1].At - _http.Requests[0].At >= TimeSpan.FromSeconds(1));
	}

	[Fact]
	public async Task An_http_error_is_a_yahoo_failure_with_the_status()
	{
		_http.Respond("busy", HttpStatusCode.TooManyRequests);

		var ex = await Assert.ThrowsAsync<YahooServiceException>(() =>
			RunAsync(_client.GetEarningsPageAsync(Thursday, 0, Ct))
		);

		Assert.Contains("429", ex.Message);
	}
}
