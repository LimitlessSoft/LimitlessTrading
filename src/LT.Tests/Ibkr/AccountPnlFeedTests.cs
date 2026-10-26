using System.Threading.Channels;
using LT.Contracts.Dtos.Account;
using LT.Ibkr;

namespace LT.Tests.Ibkr;

public class AccountPnlFeedTests
{
	private readonly Dictionary<string, Channel<AccountPnlDto>> _ib = [];
	private readonly List<string> _opened = [];
	private readonly List<string> _cancelled = [];
	private readonly AccountPnlFeed _feed;

	public AccountPnlFeedTests() =>
		_feed = new AccountPnlFeed(
			(account, ct) =>
			{
				lock (_opened)
					_opened.Add(account);
				ct.Register(() =>
				{
					lock (_cancelled)
						_cancelled.Add(account);
				});
				return Ib(account).Reader.ReadAllAsync(ct);
			}
		);

	[Fact]
	public async Task Subscribers_of_one_account_share_one_IB_subscription()
	{
		var ct = TestContext.Current.CancellationToken;
		await using var first = _feed.SubscribeAsync("DU1", ct).GetAsyncEnumerator(ct);
		await using var second = _feed.SubscribeAsync("DU1", ct).GetAsyncEnumerator(ct);
		var firstNext = first.MoveNextAsync();
		var secondNext = second.MoveNextAsync();

		await Ib("DU1").Writer.WriteAsync(Pnl(10), ct);

		Assert.True(await firstNext);
		Assert.True(await secondNext);
		Assert.Equal((10.0, 10.0), (first.Current.DailyPnl!.Value, second.Current.DailyPnl!.Value));
		Assert.Equal(["DU1"], _opened);
	}

	[Fact]
	public async Task Each_account_has_its_own_subscription()
	{
		var ct = TestContext.Current.CancellationToken;
		await using var first = _feed.SubscribeAsync("DU1", ct).GetAsyncEnumerator(ct);
		await using var second = _feed.SubscribeAsync("DU2", ct).GetAsyncEnumerator(ct);
		var firstNext = first.MoveNextAsync();
		var secondNext = second.MoveNextAsync();

		await Ib("DU1").Writer.WriteAsync(Pnl(1), ct);
		await Ib("DU2").Writer.WriteAsync(Pnl(2), ct);

		Assert.True(await firstNext);
		Assert.True(await secondNext);
		Assert.Equal((1.0, 2.0), (first.Current.DailyPnl!.Value, second.Current.DailyPnl!.Value));
		Assert.Equal(["DU1", "DU2"], _opened.Order());
	}

	[Fact]
	public async Task A_late_subscriber_gets_the_latest_pnl_first()
	{
		var ct = TestContext.Current.CancellationToken;
		await using var first = _feed.SubscribeAsync("DU1", ct).GetAsyncEnumerator(ct);
		var firstNext = first.MoveNextAsync();
		await Ib("DU1").Writer.WriteAsync(Pnl(10), ct);
		await firstNext;

		await using var late = _feed.SubscribeAsync("DU1", ct).GetAsyncEnumerator(ct);

		Assert.True(await late.MoveNextAsync());
		Assert.Equal(10, late.Current.DailyPnl);
		Assert.Single(_opened);
	}

	[Fact]
	public async Task The_IB_subscription_ends_with_the_last_subscriber()
	{
		var ct = TestContext.Current.CancellationToken;
		var first = _feed.SubscribeAsync("DU1", ct).GetAsyncEnumerator(ct);
		var second = _feed.SubscribeAsync("DU1", ct).GetAsyncEnumerator(ct);
		var firstNext = first.MoveNextAsync();
		var secondNext = second.MoveNextAsync();
		await Ib("DU1").Writer.WriteAsync(Pnl(10), ct);
		await firstNext;
		await secondNext;

		await first.DisposeAsync();
		Assert.Empty(_cancelled);

		await second.DisposeAsync();
		Assert.Equal(["DU1"], _cancelled);
	}

	[Fact]
	public async Task Disposing_ends_every_subscriber()
	{
		var ct = TestContext.Current.CancellationToken;
		await using var subscriber = _feed.SubscribeAsync("DU1", ct).GetAsyncEnumerator(ct);
		var next = subscriber.MoveNextAsync();

		await _feed.DisposeAsync();

		Assert.False(await next);
	}

	private Channel<AccountPnlDto> Ib(string account)
	{
		lock (_ib)
		{
			if (!_ib.TryGetValue(account, out var channel))
				_ib[account] = channel = Channel.CreateUnbounded<AccountPnlDto>();
			return channel;
		}
	}

	private static AccountPnlDto Pnl(double daily) => new() { DailyPnl = daily };
}
