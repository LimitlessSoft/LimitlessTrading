using System.Runtime.CompilerServices;
using System.Threading.Channels;
using LSCore.Mapper.Domain;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Account;
using TwsApi;

namespace LT.Ibkr;

/// <summary>
/// The one reqAccountSummary subscription of a connection, shared by every caller. IB allows two
/// account summary requests per client and IB Gateway never frees a slot on cancelAccountSummary
/// (error 322 from the third request on, seen 2026-10-23), so the subscription is opened once and
/// kept until the connection ends. New subscribers get the latest values first, then each change.
/// </summary>
public sealed class AccountSummaryFeed(ITwsClient client, RequestThrottle messages)
	: IAsyncDisposable
{
	private readonly Lock _lock = new();
	private readonly Dictionary<(string Account, string Tag), AccountValueDto> _values = [];
	private readonly List<Channel<AccountValueDto>> _subscribers = [];
	private readonly CancellationTokenSource _stop = new();
	private Task? _pump;

	public async IAsyncEnumerable<AccountValueDto> SubscribeAsync(
		[EnumeratorCancellation] CancellationToken cancellationToken
	)
	{
		var channel = Channel.CreateUnbounded<AccountValueDto>(
			new UnboundedChannelOptions { SingleReader = true }
		);
		lock (_lock)
		{
			foreach (var value in _values.Values)
				channel.Writer.TryWrite(value);
			_subscribers.Add(channel);
			_pump ??= Task.Run(PumpAsync);
		}

		try
		{
			await foreach (var value in channel.Reader.ReadAllAsync(cancellationToken))
				yield return value;
		}
		finally
		{
			lock (_lock)
				_subscribers.Remove(channel);
		}
	}

	public async ValueTask DisposeAsync()
	{
		await _stop.CancelAsync();
		if (_pump != null)
			await _pump;
		_stop.Dispose();
	}

	private async Task PumpAsync()
	{
		Exception? error = null;
		try
		{
			await messages.WaitAsync(_stop.Token);
			await foreach (
				var value in client.SubscribeAccountSummaryAsync(
					string.Join(",", AccountConstants.SummaryTags),
					cancellationToken: _stop.Token
				)
			)
			{
				var dto = value.ToMapped<AccountValue, AccountValueDto>();
				lock (_lock)
				{
					_values[(dto.Account, dto.Tag)] = dto;
					foreach (var subscriber in _subscribers)
						subscriber.Writer.TryWrite(dto);
				}
			}
		}
		catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
		catch (Exception ex)
		{
			error = ex;
		}

		// Ended without being disposed (error or dropped connection): the next subscriber retries
		lock (_lock)
		{
			foreach (var subscriber in _subscribers)
				subscriber.Writer.TryComplete(error);
			_subscribers.Clear();
			_values.Clear();
			_pump = null;
		}
	}
}
