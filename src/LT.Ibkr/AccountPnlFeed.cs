using System.Runtime.CompilerServices;
using System.Threading.Channels;
using LT.Contracts.Dtos.Account;

namespace LT.Ibkr;

/// <summary>
/// One reqPnL subscription per account, shared by every caller of that account (the Account
/// widget and the performance recorder both follow it). Opened by the first subscriber, cancelled
/// when the last one leaves; a new subscriber gets the latest P&amp;L first, then each update.
/// </summary>
/// <param name="subscribe">Opens one IB subscription for an account (already paced and mapped).</param>
public sealed class AccountPnlFeed(
	Func<string, CancellationToken, IAsyncEnumerable<AccountPnlDto>> subscribe
) : IAsyncDisposable
{
	private readonly Lock _lock = new();
	private readonly Dictionary<string, Subscription> _subscriptions = [];

	public async IAsyncEnumerable<AccountPnlDto> SubscribeAsync(
		string account,
		[EnumeratorCancellation] CancellationToken cancellationToken
	)
	{
		var channel = Channel.CreateUnbounded<AccountPnlDto>(
			new UnboundedChannelOptions { SingleReader = true }
		);
		Subscription subscription;
		lock (_lock)
		{
			if (!_subscriptions.TryGetValue(account, out subscription!))
			{
				subscription = new Subscription();
				_subscriptions[account] = subscription;
				subscription.Pump = Task.Run(() => PumpAsync(account, subscription));
			}
			if (subscription.Latest != null)
				channel.Writer.TryWrite(subscription.Latest);
			subscription.Subscribers.Add(channel);
		}

		try
		{
			await foreach (var pnl in channel.Reader.ReadAllAsync(cancellationToken))
				yield return pnl;
		}
		finally
		{
			var stop = false;
			lock (_lock)
			{
				subscription.Subscribers.Remove(channel);
				if (
					subscription.Subscribers.Count == 0
					&& _subscriptions.GetValueOrDefault(account) == subscription
				)
				{
					_subscriptions.Remove(account);
					stop = true;
				}
			}
			// Outside the lock: cancelling runs the IB cancel inline
			if (stop)
				await subscription.Stop.CancelAsync();
		}
	}

	public async ValueTask DisposeAsync()
	{
		List<Subscription> subscriptions;
		lock (_lock)
		{
			subscriptions = _subscriptions.Values.ToList();
			_subscriptions.Clear();
		}
		foreach (var subscription in subscriptions)
		{
			await subscription.Stop.CancelAsync();
			if (subscription.Pump != null)
				await subscription.Pump;
		}
	}

	private async Task PumpAsync(string account, Subscription subscription)
	{
		Exception? error = null;
		try
		{
			await foreach (var pnl in subscribe(account, subscription.Stop.Token))
			{
				lock (_lock)
				{
					subscription.Latest = pnl;
					foreach (var subscriber in subscription.Subscribers)
						subscriber.Writer.TryWrite(pnl);
				}
			}
		}
		catch (OperationCanceledException) when (subscription.Stop.IsCancellationRequested) { }
		catch (Exception ex)
		{
			error = ex;
		}

		// Ended on its own (error or dropped connection): the next subscriber opens a new one
		lock (_lock)
		{
			foreach (var subscriber in subscription.Subscribers)
				subscriber.Writer.TryComplete(error);
			subscription.Subscribers.Clear();
			if (_subscriptions.GetValueOrDefault(account) == subscription)
				_subscriptions.Remove(account);
		}
	}

	private sealed class Subscription
	{
		public List<Channel<AccountPnlDto>> Subscribers { get; } = [];
		public CancellationTokenSource Stop { get; } = new();
		public AccountPnlDto? Latest { get; set; }
		public Task? Pump { get; set; }
	}
}
