namespace LT.Contracts.Enums;

/// <summary>
/// The span of bars a chart shows, ending now. <see cref="Auto"/> is the timeframe's own span
/// (the chart before periods existed); then the rolling periods (now minus the span) and the
/// calendar periods (from the start of the current day, week, month or year in the display time
/// zone). Persisted: add new values at the end only.
/// </summary>
public enum ChartPeriod
{
	Auto = 0,
	OneDay = 1,
	SevenDays = 2,
	OneMonth = 3,
	ThreeMonths = 4,
	SixMonths = 5,
	OneYear = 6,
	FiveYears = 7,
	Today = 8,
	ThisWeek = 9,
	ThisMonth = 10,
	ThisYear = 11,
}
