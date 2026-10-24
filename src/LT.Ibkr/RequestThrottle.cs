namespace LT.Ibkr;

/// <summary>
/// Sliding-window limiter for IB pacing rules: at most <paramref name="maxRequests"/> requests per
/// <paramref name="window"/>. Callers wait (asynchronously) instead of getting disconnected.
/// </summary>
public sealed class RequestThrottle(int maxRequests, TimeSpan window)
{
	private readonly SemaphoreSlim _gate = new(1, 1);
	private readonly Queue<DateTimeOffset> _sent = new();

	public async Task WaitAsync(CancellationToken cancellationToken = default)
	{
		await _gate.WaitAsync(cancellationToken);
		try
		{
			while (true)
			{
				var now = DateTimeOffset.UtcNow;
				while (_sent.Count > 0 && now - _sent.Peek() >= window)
					_sent.Dequeue();

				if (_sent.Count < maxRequests)
				{
					_sent.Enqueue(now);
					return;
				}

				await Task.Delay(window - (now - _sent.Peek()), cancellationToken);
			}
		}
		finally
		{
			_gate.Release();
		}
	}
}
