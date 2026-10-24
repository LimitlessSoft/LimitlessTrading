using LT.Contracts.Constants;
using LT.Contracts.Enums;

namespace LT.Contracts.Helpers;

public static class ChartTimeframeHelpers
{
	/// <summary>IB durationStr for reqHistoricalData.</summary>
	public static string GetDuration(ChartTimeframe timeframe) =>
		timeframe switch
		{
			ChartTimeframe.OneMinute => "1 D",
			ChartTimeframe.FiveMinutes => "5 D",
			ChartTimeframe.FifteenMinutes => "10 D",
			ChartTimeframe.OneHour => "1 M",
			ChartTimeframe.OneDay => "1 Y",
			_ => "5 Y",
		};

	/// <summary>IB barSizeSetting for reqHistoricalData.</summary>
	public static string GetBarSize(ChartTimeframe timeframe) =>
		timeframe switch
		{
			ChartTimeframe.OneMinute => "1 min",
			ChartTimeframe.FiveMinutes => "5 mins",
			ChartTimeframe.FifteenMinutes => "15 mins",
			ChartTimeframe.OneHour => "1 hour",
			ChartTimeframe.OneDay => "1 day",
			_ => "1 week",
		};

	public static TimeSpan GetBarLength(ChartTimeframe timeframe) =>
		timeframe switch
		{
			ChartTimeframe.OneMinute => TimeSpan.FromMinutes(1),
			ChartTimeframe.FiveMinutes => TimeSpan.FromMinutes(5),
			ChartTimeframe.FifteenMinutes => TimeSpan.FromMinutes(15),
			ChartTimeframe.OneHour => TimeSpan.FromHours(1),
			ChartTimeframe.OneDay => TimeSpan.FromDays(1),
			_ => TimeSpan.FromDays(7),
		};

	public static bool IsIntraday(ChartTimeframe timeframe) =>
		timeframe is not (ChartTimeframe.OneDay or ChartTimeframe.OneWeek);

	/// <summary>How long cached bars are served without asking IB again.</summary>
	public static TimeSpan GetCacheTtl(ChartTimeframe timeframe) =>
		IsIntraday(timeframe) ? TimeSpan.FromMinutes(1) : TimeSpan.FromMinutes(30);

	public static string GetDisplayName(ChartTimeframe timeframe) =>
		timeframe switch
		{
			ChartTimeframe.OneMinute => "1m",
			ChartTimeframe.FiveMinutes => "5m",
			ChartTimeframe.FifteenMinutes => "15m",
			ChartTimeframe.OneHour => "1h",
			ChartTimeframe.OneDay => "1D",
			_ => "1W",
		};

	/// <summary>Forex has no trades, only quotes.</summary>
	public static string GetWhatToShow(string securityType) =>
		securityType == MarketDataConstants.ForexType ? "MIDPOINT" : "TRADES";
}
