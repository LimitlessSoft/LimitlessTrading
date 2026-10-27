using System.Net;
using LT.Contracts.Enums;
using LT.Contracts.Exceptions;
using LT.Nasdaq;
using LT.Tests.Fakes;
using Microsoft.Extensions.Time.Testing;

namespace LT.Tests.Nasdaq;

public class NasdaqApiClientTests
{
	private static readonly DateOnly Monday = new(2026, 10, 26);

	private readonly FakeTimeProvider _time = new(
		new DateTimeOffset(2026, 10, 27, 14, 0, 0, TimeSpan.Zero)
	);
	private readonly FakeHttpHandler _http;
	private readonly NasdaqApiClient _client;

	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	public NasdaqApiClientTests()
	{
		LSCoreContainer.EnsureInitialized();
		_http = new FakeHttpHandler(_time);
		_client = new NasdaqApiClient(NasdaqApiClient.CreateHttpClient(_http), _time);
	}

	// Moves the fake clock until the call finishes (the request spacing waits on it)
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
	public async Task Asks_each_calendar_for_the_day_with_a_browser_user_agent()
	{
		_http.Respond(NasdaqFixtures.Earnings).Respond(NasdaqFixtures.Splits);

		var earnings = await RunAsync(
			_client.GetCalendarAsync(NasdaqCalendar.Earnings, Monday, Ct)
		);
		await RunAsync(_client.GetCalendarAsync(NasdaqCalendar.Splits, Monday, Ct));

		Assert.Equal(3, earnings.Count);
		Assert.Equal(
			"https://api.nasdaq.com/api/calendar/earnings?date=2026-10-26",
			_http.Requests[0].Uri.ToString()
		);
		Assert.Equal(
			"https://api.nasdaq.com/api/calendar/splits?date=2026-10-26",
			_http.Requests[1].Uri.ToString()
		);
		Assert.StartsWith("Mozilla/5.0", _http.Requests[0].UserAgent);
		Assert.Contains(
			"application/json",
			NasdaqApiClient.CreateHttpClient().DefaultRequestHeaders.Accept.ToString()
		);
	}

	// Nasdaq's economic calendar is a day behind its date parameter
	[Fact]
	public async Task Economic_releases_of_a_day_are_asked_for_with_the_next_date()
	{
		_http.Respond(NasdaqFixtures.Economic);

		var events = await RunAsync(_client.GetCalendarAsync(NasdaqCalendar.Economic, Monday, Ct));

		Assert.EndsWith("/economicevents?date=2026-10-27", _http.Requests[0].Uri.ToString());
		Assert.All(events, x => Assert.Equal(Monday, x.Date));
	}

	[Fact]
	public async Task Requests_are_spaced()
	{
		_http.Respond(NasdaqFixtures.EarningsWeekend);

		var first = _client.GetCalendarAsync(NasdaqCalendar.Earnings, Monday, Ct);
		var second = _client.GetCalendarAsync(NasdaqCalendar.Earnings, Monday.AddDays(1), Ct);
		await RunAsync(Task.WhenAll(first, second).ContinueWith(_ => 0, Ct));

		Assert.Equal(2, _http.Requests.Count);
		Assert.True(_http.Requests[1].At - _http.Requests[0].At >= TimeSpan.FromMilliseconds(250));
	}

	[Fact]
	public async Task An_HTTP_error_says_its_status()
	{
		_http.Respond("Access Denied", HttpStatusCode.Forbidden);

		var error = await Assert.ThrowsAsync<NasdaqServiceException>(() =>
			RunAsync(_client.GetCalendarAsync(NasdaqCalendar.Earnings, Monday, Ct))
		);

		Assert.Contains("403", error.Message);
	}

	[Fact]
	public async Task A_network_error_is_a_Nasdaq_error()
	{
		_http.Throw(new HttpRequestException("No such host is known. (api.nasdaq.com:443)"));

		var error = await Assert.ThrowsAsync<NasdaqServiceException>(() =>
			RunAsync(_client.GetCalendarAsync(NasdaqCalendar.Earnings, Monday, Ct))
		);

		Assert.Contains("No such host", error.Message);
	}
}
