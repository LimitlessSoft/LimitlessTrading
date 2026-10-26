using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace LT.Contracts.Helpers;

public static class AsyncEnumerableHelpers
{
	/// <summary>
	/// Items of every stream as they arrive, until all have ended. The first stream to fail ends
	/// the merge with its error and stops the others; so does cancelling.
	/// </summary>
	public static async IAsyncEnumerable<T> MergeAsync<T>(
		IReadOnlyList<IAsyncEnumerable<T>> streams,
		[EnumeratorCancellation] CancellationToken cancellationToken = default
	)
	{
		if (streams.Count == 1)
		{
			await foreach (
				var item in streams[0].WithCancellation(cancellationToken).ConfigureAwait(false)
			)
				yield return item;
			yield break;
		}

		using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		var channel = Channel.CreateUnbounded<T>(
			new UnboundedChannelOptions { SingleReader = true }
		);
		var remaining = streams.Count;
		var pumps = streams
			.Select(stream =>
				Task.Run(
					async () =>
					{
						try
						{
							await foreach (
								var item in stream
									.WithCancellation(stop.Token)
									.ConfigureAwait(false)
							)
								await channel
									.Writer.WriteAsync(item, stop.Token)
									.ConfigureAwait(false);
							if (Interlocked.Decrement(ref remaining) == 0)
								channel.Writer.TryComplete();
						}
						catch (Exception ex)
						{
							channel.Writer.TryComplete(ex);
						}
					},
					CancellationToken.None
				)
			)
			.ToList();

		try
		{
			await foreach (
				var item in channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false)
			)
				yield return item;
		}
		finally
		{
			await stop.CancelAsync().ConfigureAwait(false);
			await Task.WhenAll(pumps).ConfigureAwait(false);
		}
	}
}
