namespace LT.Contracts.Helpers;

public static class CollectionHelpers
{
	/// <summary>
	/// Rearranges <paramref name="list"/> (an observable collection a grid shows) into
	/// <paramref name="order"/>, the same items, with as few remove + insert pairs as possible:
	/// the longest run already in order stays put. Returns how many items moved (0 when the order
	/// is unchanged, so an unchanged sort touches nothing). A grid that only understands add and
	/// remove notifications (Avalonia's DataGrid ignores Move) follows along.
	/// </summary>
	public static int ApplyOrder<T>(IList<T> list, IReadOnlyList<T> order)
		where T : notnull
	{
		if (list.Count != order.Count)
			throw new ArgumentException("The order must hold the list's items.", nameof(order));

		var target = new Dictionary<T, int>(order.Count);
		for (var i = 0; i < order.Count; i++)
			target[order[i]] = i;

		var positions = new int[list.Count];
		for (var i = 0; i < list.Count; i++)
			positions[i] = target.TryGetValue(list[i], out var position)
				? position
				: throw new ArgumentException(
					"The order must hold the list's items.",
					nameof(order)
				);

		var stay = GetLongestIncreasingRun(positions);
		if (stay.Count == list.Count)
			return 0;

		for (var i = list.Count - 1; i >= 0; i--)
			if (!stay.Contains(positions[i]))
				list.RemoveAt(i);
		// Inserting by target position: everything before it is already in place
		for (var i = 0; i < order.Count; i++)
			if (!stay.Contains(i))
				list.Insert(i, order[i]);
		return order.Count - stay.Count;
	}

	// The values of one longest strictly increasing subsequence (patience sorting)
	private static HashSet<int> GetLongestIncreasingRun(int[] values)
	{
		var tails = new List<int>(); // index into values of the smallest tail per length
		var previous = new int[values.Length];
		for (var i = 0; i < values.Length; i++)
		{
			int low = 0,
				high = tails.Count;
			while (low < high)
			{
				var mid = (low + high) / 2;
				if (values[tails[mid]] < values[i])
					low = mid + 1;
				else
					high = mid;
			}
			previous[i] = low > 0 ? tails[low - 1] : -1;
			if (low == tails.Count)
				tails.Add(i);
			else
				tails[low] = i;
		}

		var run = new HashSet<int>();
		for (var i = tails.Count > 0 ? tails[^1] : -1; i >= 0; i = previous[i])
			run.Add(values[i]);
		return run;
	}
}
