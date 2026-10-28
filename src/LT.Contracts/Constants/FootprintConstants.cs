namespace LT.Contracts.Constants;

/// <summary>Footprint bars (<see cref="Enums.ChartBarStyle.Footprint"/>) and their trade backfill.</summary>
public static class FootprintConstants
{
	/// <summary>The most trades one IB historical ticks request returns (IB refuses more: 10187).</summary>
	public const int MaxTicksPerRequest = 1000;

	/// <summary>
	/// Historical ticks requests one chart load sends at most to backfill its footprints, newest
	/// bars first (about 1000 trades each; AAPL trades that many in about 30 s at the open).
	/// Bars past what they reach show as candles marked "no footprint".
	/// </summary>
	public const int MaxBackfillRequests = 40;

	/// <summary>
	/// Diagonal imbalance: a row's ask is an imbalance once it is at least this share of the bid
	/// one row down (a row's bid against the ask one row up), in percent (Settings → Charts).
	/// </summary>
	public const int DefaultImbalancePercent = 300;
	public const int ImbalancePercentMin = 110;
	public const int ImbalancePercentMax = 1000;

	/// <summary>Price step when IB gives the contract no usable minimum tick.</summary>
	public const double FallbackTickSize = 0.01;
}
