namespace LT.Contracts.Helpers;

/// <summary>
/// Which watchlist rows stream live (those on screen, plus a buffer) and when an off-screen row
/// is due a snapshot.
/// </summary>
public static class WatchlistStreamingHelpers
{
	/// <summary>
	/// The rows to stream live, as [Start, End): from the first to the last shown row (indices in
	/// display order), <paramref name="buffer"/> more on each side, within the list. Null when no
	/// row is shown (the widget is hidden or the list empty).
	/// </summary>
	public static (int Start, int End)? GetLiveRange(
		int rowCount,
		IEnumerable<int> shownIndices,
		int buffer
	)
	{
		int? first = null,
			last = null;
		foreach (var index in shownIndices)
		{
			if (index < 0 || index >= rowCount)
				continue;
			first = first is { } f ? Math.Min(f, index) : index;
			last = last is { } l ? Math.Max(l, index) : index;
		}
		if (first is not { } start || last is not { } end)
			return null;
		return (Math.Max(0, start - buffer), Math.Min(rowCount, end + 1 + buffer));
	}

	/// <summary>
	/// An off-screen row needs a snapshot: it has no quote yet, or its last one (live or snapshot)
	/// is <paramref name="refreshInterval"/> old; not before <paramref name="retryAt"/> after a
	/// failed one.
	/// </summary>
	public static bool IsSnapshotDue(
		DateTimeOffset? lastQuoteAt,
		DateTimeOffset? retryAt,
		DateTimeOffset now,
		TimeSpan refreshInterval
	) =>
		(retryAt is not { } retry || now >= retry)
		&& (lastQuoteAt is not { } last || now - last >= refreshInterval);
}
