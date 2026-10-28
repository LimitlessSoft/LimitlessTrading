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
	public async Task The_latest_filing_instance_is_found_through_the_submissions()
	{
		_http
			.Respond(
				"""
				{"filings":{"recent":{"accessionNumber":["0001193125-26-341032"],"filingDate":["2026-08-10"],
				 "form":["10-Q"],"primaryDocument":["brka-20260630.htm"],"isInlineXBRL":[1]}}}
				"""
			)
			.Respond(
				"""
				<xbrl xmlns="http://www.xbrl.org/2003/instance" xmlns:dei="http://xbrl.sec.gov/dei/2025">
				  <context id="c"><entity><identifier scheme="x">1</identifier></entity><period><instant>2026-07-29</instant></period></context>
				  <dei:EntityCommonStockSharesOutstanding contextRef="c">5</dei:EntityCommonStockSharesOutstanding>
				</xbrl>
				"""
			);

		var facts = await RunAsync(
			_client.GetLatestFilingShareFactsAsync(1067983, TestContext.Current.CancellationToken)
		);

		Assert.Equal(5, Assert.Single(facts!).Value);
		Assert.Equal("10-Q", facts![0].Form);
		Assert.EndsWith("/submissions/CIK0001067983.json", _http.Requests[0].Uri.AbsolutePath);
		Assert.Equal(
			"/Archives/edgar/data/1067983/000119312526341032/brka-20260630_htm.xml",
			_http.Requests[1].Uri.AbsolutePath
		);
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
