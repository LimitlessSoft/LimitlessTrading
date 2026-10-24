using LT.Contracts.Dtos.Account;
using LT.Ibkr;
using LT.Tests.Fakes;
using TwsApi;

namespace LT.Tests.Ibkr;

public class AccountSummaryFeedTests
{
	private readonly (ITwsClient Client, FakeTwsClient Fake) _tws = FakeTwsClient.Create();
	private readonly AccountSummaryFeed _feed;

	public AccountSummaryFeedTests()
	{
		LSCoreContainer.EnsureInitialized();
		_feed = new AccountSummaryFeed(
			_tws.Client,
			new RequestThrottle(50, TimeSpan.FromSeconds(1))
		);
	}

	[Fact]
	public async Task Subscribers_share_one_IB_subscription()
	{
		var ct = TestContext.Current.CancellationToken;
		await using var first = _feed.SubscribeAsync(ct).GetAsyncEnumerator(ct);
		await using var second = _feed.SubscribeAsync(ct).GetAsyncEnumerator(ct);
		var firstNext = first.MoveNextAsync();
		var secondNext = second.MoveNextAsync();

		await _tws.Fake.AccountSummary.Writer.WriteAsync(Value("100"), ct);

		Assert.True(await firstNext);
		Assert.True(await secondNext);
		Assert.Equal(("100", "100"), (first.Current.Value, second.Current.Value));
		Assert.Equal(1, _tws.Fake.AccountSummaryRequests);
	}

	[Fact]
	public async Task A_late_subscriber_gets_the_latest_values_first()
	{
		var ct = TestContext.Current.CancellationToken;
		await using var first = _feed.SubscribeAsync(ct).GetAsyncEnumerator(ct);
		var firstNext = first.MoveNextAsync();
		await _tws.Fake.AccountSummary.Writer.WriteAsync(Value("100"), ct);
		await firstNext;
		await _tws.Fake.AccountSummary.Writer.WriteAsync(Value("250"), ct);
		await first.MoveNextAsync();

		await using var late = _feed.SubscribeAsync(ct).GetAsyncEnumerator(ct);

		Assert.True(await late.MoveNextAsync());
		Assert.Equal("250", late.Current.Value);
		Assert.Equal(1, _tws.Fake.AccountSummaryRequests);
	}

	[Fact]
	public async Task Disposing_ends_every_subscriber()
	{
		var ct = TestContext.Current.CancellationToken;
		await using var subscriber = _feed.SubscribeAsync(ct).GetAsyncEnumerator(ct);
		var next = subscriber.MoveNextAsync();

		await _feed.DisposeAsync();

		Assert.False(await next);
	}

	private static AccountValue Value(string value) => new("DU1", "NetLiquidation", value, "USD");
}
