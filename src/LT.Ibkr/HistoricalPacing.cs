using LT.Contracts.Constants;

namespace LT.Ibkr;

/// <summary>
/// IB's historical data pacing: at most 60 requests per 10 minutes, but only for bars of 30
/// seconds or less (IB rejects those with a pacing violation). Larger bars — every chart
/// timeframe — have no hard limit: IB slows its answers down itself once many come in, so they
/// are not held back here (holding them left charts waiting minutes on "Loading…"). Historical
/// ticks have no window either: IB answers ~10 pages at once, then holds its answers and releases
/// them on a 6-second clock (~22 pages a minute), never with a pacing error, and bar requests
/// meanwhile are answered as fast as ever. Several ticks requests at once are held far longer
/// (up to ~100 s), so they go one at a time (see docs/ibkr-tws-api.md#historical-ticks).
/// </summary>
public sealed class HistoricalPacing(TimeProvider? timeProvider = null)
{
	private readonly RequestThrottle _smallBars = new(
		MarketDataConstants.MaxHistoricalRequests,
		MarketDataConstants.HistoricalWindow,
		timeProvider
	);

	/// <summary>Waits while a request for <paramref name="barSize"/> bars would break the pacing.</summary>
	public Task WaitAsync(string barSize, CancellationToken cancellationToken = default) =>
		IsSmallBarSize(barSize) ? _smallBars.WaitAsync(cancellationToken) : Task.CompletedTask;

	private readonly SemaphoreSlim _ticks = new(1, 1);

	/// <summary>
	/// Waits for the historical ticks request in flight to be answered (one at a time); call
	/// <see cref="ReleaseTicks"/> once this one is.
	/// </summary>
	public Task WaitTicksAsync(CancellationToken cancellationToken = default) =>
		_ticks.WaitAsync(cancellationToken);

	/// <summary>The historical ticks request let through by <see cref="WaitTicksAsync"/> is answered.</summary>
	public void ReleaseTicks() => _ticks.Release();

	/// <summary>An IB barSizeSetting in seconds ("1 secs" … "30 secs"): the paced ones.</summary>
	public static bool IsSmallBarSize(string barSize) =>
		barSize.TrimEnd().EndsWith("secs", StringComparison.OrdinalIgnoreCase)
		|| barSize.TrimEnd().EndsWith("sec", StringComparison.OrdinalIgnoreCase);
}
