namespace LT.Contracts.Constants;

/// <summary>Footprint bars (<see cref="Enums.ChartBarStyle.Footprint"/>) and their trade backfill.</summary>
public static class FootprintConstants
{
	/// <summary>The most trades one IB historical ticks request returns (IB refuses more: 10187).</summary>
	public const int MaxTicksPerRequest = 1000;

	/// <summary>
	/// Historical ticks requests one chart load sends at most to backfill its footprints (about
	/// 1000 trades each). SPY traded ~220,000 times from 13:05 to 16:00 New York on 2026-10-29
	/// (213 pages), so a whole session of a heavy symbol is ~500 pages; IB answers ~22 a minute
	/// once its first ~10 are in. Bars past what they reach show as candles marked "no footprint".
	/// </summary>
	public const int MaxBackfillRequests = 1200;

	/// <summary>
	/// Diagonal imbalance: a row's ask is an imbalance once it is at least this share of the bid
	/// one row down (a row's bid against the ask one row up), in percent (Settings → Charts).
	/// </summary>
	public const int DefaultImbalancePercent = 300;
	public const int ImbalancePercentMin = 110;
	public const int ImbalancePercentMax = 1000;

	/// <summary>Price step when IB gives the contract no usable minimum tick.</summary>
	public const double FallbackTickSize = 0.01;

	/// <summary>
	/// The footprint trade cache's folder in the app data (<c>AppPathHelpers.GetFootprintTapeDirectory</c>):
	/// <c>footprint-trades/&lt;conId&gt;/&lt;yyyy-MM-dd&gt;.lttape</c>, UTC days.
	/// </summary>
	public const string TapeDirectoryName = "footprint-trades";

	public const string TapeFileExtension = ".lttape";

	/// <summary>Days of trades kept; older days are deleted.</summary>
	public const int TapeRetentionDays = 30;

	/// <summary>
	/// The cache's size cap; the oldest days go first. SPY is ~4 MB a day (~1 M trades), most
	/// symbols far less.
	/// </summary>
	public const long TapeMaxBytes = 2L * 1024 * 1024 * 1024;

	/// <summary>
	/// Live trades are written to the cache in batches this long (a crash loses at most this much
	/// of the recording, which the next load pages from IB).
	/// </summary>
	public static readonly TimeSpan TapeLiveFlushInterval = TimeSpan.FromSeconds(2);

	/// <summary>
	/// A page of past trades is recorded to the cache only up to this long before now: IB's
	/// history may not hold the last moment's trades yet, and a stretch once recorded is never
	/// asked for again. The next load pages the rest.
	/// </summary>
	public static readonly TimeSpan TapeHistoricalSettle = TimeSpan.FromSeconds(30);

	/// <summary>
	/// Cached trades are counted into a chart's footprints this many at a time, the UI drawing in
	/// between (a heavy symbol's day is ~1 M).
	/// </summary>
	public const int TapeTradesPerSlice = 100_000;
}
