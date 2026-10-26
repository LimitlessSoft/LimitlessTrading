using LT.Contracts.Constants;

namespace LT.Ibkr;

/// <summary>
/// IB's historical data pacing: at most 60 requests per 10 minutes, but only for bars of 30
/// seconds or less (IB rejects those with a pacing violation). Larger bars — every chart
/// timeframe — have no hard limit: IB slows its answers down itself once many come in, so they
/// are not held back here (holding them left charts waiting minutes on "Loading…").
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

	/// <summary>An IB barSizeSetting in seconds ("1 secs" … "30 secs"): the paced ones.</summary>
	public static bool IsSmallBarSize(string barSize) =>
		barSize.TrimEnd().EndsWith("secs", StringComparison.OrdinalIgnoreCase)
		|| barSize.TrimEnd().EndsWith("sec", StringComparison.OrdinalIgnoreCase);
}
