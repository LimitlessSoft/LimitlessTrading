using System.Threading.Channels;
using LT.Contracts.Dtos.Account;
using LT.Ibkr;

namespace LT.Tests.Ibkr;

public class AccountSummaryFeedTests
{
	private readonly Channel<AccountValueDto> _ib = Channel.CreateUnbounded<AccountValueDto>();
	private readonly AccountSummaryFeed _feed;
	private int _subscriptions;

	public AccountSummaryFeedTests() =>
		_feed = new AccountSummaryFeed(ct =>
		{
			Interlocked.Increment(ref _subscriptions);
			return _ib.Reader.ReadAllAsync(ct);
		});

	[Fact]
	public async Task Subscribers_share_one_IB_subscription()
	{
		var ct = TestContext.Current.CancellationToken;
		await using var first = _feed.SubscribeAsync(ct).GetAsyncEnumerator(ct);
		await using var second = _feed.SubscribeAsync(ct).GetAsyncEnumerator(ct);
		var firstNext = first.MoveNextAsync();
		var secondNext = second.MoveNextAsync();

		await _ib.Writer.WriteAsync(Value("100"), ct);

		Assert.True(await firstNext);
		Assert.True(await secondNext);
		Assert.Equal(("100", "100"), (first.Current.Value, second.Current.Value));
		Assert.Equal(1, _subscriptions);
	}

	[Fact]
	public async Task A_late_subscriber_gets_the_latest_values_first()
	{
		var ct = TestContext.Current.CancellationToken;
		await using var first = _feed.SubscribeAsync(ct).GetAsyncEnumerator(ct);
		var firstNext = first.MoveNextAsync();
		await _ib.Writer.WriteAsync(Value("100"), ct);
		await firstNext;
		await _ib.Writer.WriteAsync(Value("250"), ct);
		await first.MoveNextAsync();

		await using var late = _feed.SubscribeAsync(ct).GetAsyncEnumerator(ct);

		Assert.True(await late.MoveNextAsync());
		Assert.Equal("250", late.Current.Value);
		Assert.Equal(1, _subscriptions);
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

	private static AccountValueDto Value(string value) =>
		new()
		{
			Account = "DU1",
			Tag = "NetLiquidation",
			Value = value,
			Currency = "USD",
		};
}
