namespace LT.Contracts.Dtos.Charts;

/// <summary>
/// The time window a chart's period shows (ChartPeriodHelpers.GetWindow): only bars from
/// <see cref="Start"/> on are drawn, and a calendar period's window is laid out whole, its bar
/// slots still to come left empty.
/// </summary>
public class ChartWindowDto
{
	/// <summary>Where the period starts: the first bar shown is the first ending after it.</summary>
	public DateTimeOffset Start { get; set; }

	/// <summary>
	/// Where a calendar period ends (the close of its last session, or the end of its last day);
	/// a bar from here on belongs to the next period. Null for a window ending at the latest bar
	/// (rolling periods, a period cut to the timeframe's longest, intraday without known hours).
	/// </summary>
	public DateTimeOffset? End { get; set; }

	/// <summary>
	/// The start of every bar slot of the window, oldest first: session time only for intraday
	/// bars (in the exchange's sessions), trading days for daily bars, weeks for weekly. Null when
	/// <see cref="End"/> is; empty when the period has no session (a weekend's Today).
	/// </summary>
	public IReadOnlyList<DateTimeOffset>? SlotTimes { get; set; }
}
