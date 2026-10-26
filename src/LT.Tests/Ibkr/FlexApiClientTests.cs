using System.Net;
using LT.Contracts.Constants;
using LT.Contracts.Exceptions;
using LT.Contracts.Requests.Flex;
using LT.Ibkr.Flex;
using LT.Tests.Fakes;
using Microsoft.Extensions.Time.Testing;

namespace LT.Tests.Ibkr;

public class FlexApiClientTests
{
	private const string Token = "123456789012345678901234";

	private readonly FakeTimeProvider _time = new(
		new DateTimeOffset(2026, 10, 26, 14, 0, 0, TimeSpan.Zero)
	);
	private readonly FakeHttpHandler _http;
	private readonly FlexApiClient _client;

	public FlexApiClientTests()
	{
		Fakes.LSCoreContainer.EnsureInitialized();
		_http = new FakeHttpHandler(_time);
		_client = new FlexApiClient(FlexApiClient.CreateHttpClient(_http), _time);
	}

	private static FlexStatementRequest Request(DateOnly? from = null, DateOnly? to = null) =>
		new()
		{
			Token = Token,
			QueryId = "987654",
			From = from,
			To = to,
		};

	// Moves the fake clock until the call finishes (every wait in the client is on it)
	private async Task<T> RunAsync<T>(Task<T> task)
	{
		for (var i = 0; i < 5000 && !task.IsCompleted; i++)
		{
			await Task.Delay(1, TestContext.Current.CancellationToken);
			_time.Advance(TimeSpan.FromMilliseconds(500));
		}
		return await task;
	}

	[Fact]
	public async Task Sends_the_request_then_polls_the_statement_until_it_is_generated()
	{
		_http
			.Respond(FlexFixtures.SendSuccess("555"))
			.Respond(FlexFixtures.Fail(1019, "Statement generation in progress."))
			.Respond(FlexFixtures.TradeConfirm);

		var statement = await RunAsync(
			_client.GetStatementAsync(
				Request(new DateOnly(2025, 10, 26), new DateOnly(2026, 10, 25)),
				TestContext.Current.CancellationToken
			)
		);

		Assert.Single(statement.Trades);
		Assert.Equal(3, _http.Requests.Count);
		var send = _http.Requests[0].Uri;
		Assert.Equal(
			$"{FlexConstants.BaseUrl}SendRequest?t={Token}&q=987654&fd=20251026&td=20261025&v=3",
			send.ToString()
		);
		Assert.Equal(
			$"{FlexConstants.BaseUrl}GetStatement?t={Token}&q=555&v=3",
			_http.Requests[1].Uri.ToString()
		);
		Assert.StartsWith(FlexConstants.UserAgentProduct + "/", _http.Requests[0].UserAgent);
	}

	[Fact]
	public async Task Requests_are_spaced_within_ibs_limits()
	{
		_http
			.Respond(FlexFixtures.SendSuccess())
			.Respond(FlexFixtures.Fail(1019, "In progress"))
			.Respond(FlexFixtures.Fail(1019, "In progress"))
			.Respond(FlexFixtures.Fail(1019, "In progress"))
			.Respond(FlexFixtures.Empty);

		await RunAsync(_client.GetStatementAsync(Request(), TestContext.Current.CancellationToken));

		var times = _http.Requests.Select(x => x.At).ToList();
		for (var i = 1; i < times.Count; i++)
			Assert.True(times[i] - times[i - 1] >= FlexConstants.RequestSpacing);
	}

	[Fact]
	public async Task No_more_than_the_per_minute_limit_goes_out_in_a_minute()
	{
		for (var i = 0; i < 6; i++)
			_http.Respond(FlexFixtures.SendSuccess()).Respond(FlexFixtures.Empty);

		for (var i = 0; i < 6; i++)
			await RunAsync(
				_client.GetStatementAsync(Request(), TestContext.Current.CancellationToken)
			);

		var times = _http.Requests.Select(x => x.At).ToList();
		for (var i = FlexConstants.MaxRequestsPerMinute; i < times.Count; i++)
			Assert.True(
				times[i] - times[i - FlexConstants.MaxRequestsPerMinute] >= TimeSpan.FromMinutes(1)
			);
	}

	[Fact]
	public async Task An_expired_token_fails_with_a_clear_message_without_the_token()
	{
		_http.Respond(FlexFixtures.Fail(1012, "Token has expired."));

		var error = await Assert.ThrowsAsync<FlexServiceException>(() =>
			RunAsync(_client.GetStatementAsync(Request(), TestContext.Current.CancellationToken))
		);

		Assert.Equal(1012, error.ErrorCode);
		Assert.Contains("expired", error.Message);
		Assert.DoesNotContain(Token, error.Message);
		Assert.Single(_http.Requests);
	}

	[Fact]
	public async Task Too_many_requests_waits_and_tries_again()
	{
		_http
			.Respond(FlexFixtures.Fail(1018, "Too many requests"))
			.Respond(FlexFixtures.SendSuccess())
			.Respond(FlexFixtures.Empty);

		await RunAsync(_client.GetStatementAsync(Request(), TestContext.Current.CancellationToken));

		Assert.True(
			_http.Requests[1].At - _http.Requests[0].At >= FlexConstants.TooManyRequestsDelay
		);
	}

	[Fact]
	public async Task A_statement_that_never_gets_ready_times_out()
	{
		_http.Respond(FlexFixtures.SendSuccess()).Respond(FlexFixtures.Fail(1019, "In progress"));

		var error = await Assert.ThrowsAsync<FlexServiceException>(() =>
			RunAsync(_client.GetStatementAsync(Request(), TestContext.Current.CancellationToken))
		);

		Assert.Null(error.ErrorCode);
		Assert.Contains("wasn't ready", error.Message);
	}

	[Fact]
	public async Task The_token_is_never_sent_to_a_host_that_isnt_ibs()
	{
		_http
			.Respond(
				FlexFixtures
					.SendSuccess()
					.Replace("https://ndcdyn.interactivebrokers.com", "https://evil.example.com")
			)
			.Respond(FlexFixtures.Empty);

		await RunAsync(_client.GetStatementAsync(Request(), TestContext.Current.CancellationToken));

		Assert.All(_http.Requests, x => Assert.EndsWith(".interactivebrokers.com", x.Uri.Host));
	}

	[Fact]
	public async Task Http_and_format_failures_become_flex_errors()
	{
		_http.Respond("oops", HttpStatusCode.InternalServerError);
		await Assert.ThrowsAsync<FlexServiceException>(() =>
			RunAsync(_client.GetStatementAsync(Request(), TestContext.Current.CancellationToken))
		);

		_http.Respond("<html>not flex</html>");
		var error = await Assert.ThrowsAsync<FlexServiceException>(() =>
			RunAsync(_client.GetStatementAsync(Request(), TestContext.Current.CancellationToken))
		);
		Assert.Contains("isn't a Flex statement", error.Message);
	}
}
