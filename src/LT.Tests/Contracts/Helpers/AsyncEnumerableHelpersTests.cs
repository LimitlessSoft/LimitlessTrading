using System.Runtime.CompilerServices;
using LT.Contracts.Helpers;

namespace LT.Tests.Contracts.Helpers;

public class AsyncEnumerableHelpersTests
{
	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	private static async IAsyncEnumerable<int> Items(IEnumerable<int> items, Exception? end = null)
	{
		foreach (var item in items)
		{
			await Task.Yield();
			yield return item;
		}
		if (end != null)
			throw end;
	}

	[Fact]
	public async Task Merge_yields_every_item_of_every_stream()
	{
		var items = await AsyncEnumerableHelpers
			.MergeAsync([Items([1, 2]), Items([3]), Items([])], Ct)
			.ToListAsync(Ct);

		Assert.Equal([1, 2, 3], items.Order());
	}

	[Fact]
	public async Task Merge_fails_when_a_stream_fails()
	{
		var error = new InvalidOperationException("boom");

		var thrown = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
			await AsyncEnumerableHelpers
				.MergeAsync([Items([1]), Items([2], error)], Ct)
				.ToListAsync(Ct)
		);

		Assert.Same(error, thrown);
	}

	[Fact]
	public async Task Merge_stops_the_streams_when_cancelled()
	{
		using var cancel = CancellationTokenSource.CreateLinkedTokenSource(Ct);
		var endless = new[] { Endless(Ct), Endless(Ct) };

		await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
		{
			await foreach (var _ in AsyncEnumerableHelpers.MergeAsync(endless, cancel.Token))
				await cancel.CancelAsync();
		});
	}

	private static async IAsyncEnumerable<int> Endless(
		[EnumeratorCancellation] CancellationToken cancellationToken = default
	)
	{
		yield return 1;
		await Task.Delay(Timeout.Infinite, cancellationToken);
	}
}
