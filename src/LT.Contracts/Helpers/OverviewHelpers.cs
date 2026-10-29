using System.Globalization;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Contracts;

namespace LT.Contracts.Helpers;

/// <summary>
/// What the Overview widget's Basic info tab says about a contract: its type in words, whether
/// its market is open now and its hours today (from IB's trading / liquid hours, in the
/// exchange's zone), the exchanges it routes to.
/// </summary>
public static class OverviewHelpers
{
	/// <summary>IB's security type in words ("STK" → "Stock"); an unknown one as IB wrote it.</summary>
	public static string GetSecurityTypeName(string? securityType) =>
		securityType?.Trim().ToUpperInvariant() switch
		{
			MarketDataConstants.StockType => "Stock",
			MarketDataConstants.OptionType => "Option",
			MarketDataConstants.FutureType => "Future",
			MarketDataConstants.FutureOptionType => "Future option",
			MarketDataConstants.ForexType => "Forex",
			MarketDataConstants.IndexType => "Index",
			MarketDataConstants.CryptoType => "Crypto",
			"BOND" => "Bond",
			"FUND" => "Mutual fund",
			"CMDTY" => "Commodity",
			"WAR" => "Warrant",
			"CFD" => "CFD",
			"BAG" => "Combo",
			null or "" => string.Empty,
			var other => other,
		};

	/// <summary>
	/// The market right now, in the exchange's zone: "Open · closes 16:00", "Pre-market · opens
	/// 09:30", "After hours · until 20:00", "Closed · opens Mon 04:00" (a day name when it isn't
	/// today), or "Closed" with no listed session ahead. Regular sessions come from
	/// <paramref name="liquidHours"/>, extended ones from <paramref name="tradingHours"/>. Null
	/// when IB listed no usable hours or the zone is unknown.
	/// </summary>
	public static string? DescribeMarketState(
		string? tradingHours,
		string? liquidHours,
		string? timeZoneId,
		DateTimeOffset now
	)
	{
		if (TradingHoursHelpers.FindZone(timeZoneId) is not { } zone)
			return null;
		var regular = GetListedSessions(liquidHours, zone);
		var trading = GetListedSessions(tradingHours, zone);
		if (trading.Count == 0)
			trading = regular;
		if (trading.Count == 0)
			return null;

		var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);
		foreach (var (start, end) in regular)
			if (now >= start && now < end)
				return $"Open · closes {FormatPoint(end, zone, today)}";

		foreach (var (start, end) in trading)
		{
			if (now < start || now >= end)
				continue;
			// Before the regular session of this extended one, or after it
			var opens = regular.Where(x => x.Start > now && x.Start < end).Select(x => x.Start);
			return opens.Any()
				? $"Pre-market · opens {FormatPoint(opens.Min(), zone, today)}"
				: $"After hours · until {FormatPoint(end, zone, today)}";
		}

		var next = trading.Concat(regular).Where(x => x.Start > now).Select(x => x.Start);
		return next.Any() ? $"Closed · opens {FormatPoint(next.Min(), zone, today)}" : "Closed";
	}

	/// <summary>
	/// The sessions IB listed for the exchange's current day, e.g. "09:30–16:00" (several joined
	/// with ", "; one running into the next day ends "16:00 +1"), "Closed today" for a day listed
	/// closed. Null when today isn't listed, IB sent no hours or the zone is unknown.
	/// </summary>
	public static string? DescribeToday(string? hours, string? timeZoneId, DateTimeOffset now)
	{
		if (
			TradingHoursHelpers.Parse(hours, timeZoneId) is not { } schedule
			|| TradingHoursHelpers.FindZone(timeZoneId) is not { } zone
		)
			return null;
		var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);
		if (!schedule.Days.TryGetValue(today, out var sessions))
			return null;
		if (sessions.Count == 0)
			return "Closed today";
		return string.Join(
			", ",
			sessions.Select(x =>
			{
				var start = today.ToDateTime(x.Start);
				var end = start + x.Length;
				var days = (DateOnly.FromDateTime(end).DayNumber - today.DayNumber);
				var text = $"{start:HH\\:mm}–{end:HH\\:mm}";
				return days > 0 ? $"{text} +{days}" : text;
			})
		);
	}

	/// <summary>IB's comma-separated valid exchanges as a list, blanks and repeats dropped.</summary>
	public static IReadOnlyList<string> GetValidExchanges(string? validExchanges) =>
		(validExchanges ?? string.Empty)
			.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToList();

	/// <summary>A price increment without trailing zeros: 0.01, 0.0001, 5.</summary>
	public static string FormatMinTick(double minTick) =>
		minTick > 0 ? minTick.ToString("0.##########", CultureInfo.InvariantCulture) : string.Empty;

	// Every session of the days IB listed, as instants (IB lists a few days around today);
	// sessions that touch are one (a market open around the clock lists 00:00–24:00 each day)
	private static List<(DateTimeOffset Start, DateTimeOffset End)> GetListedSessions(
		string? hours,
		TimeZoneInfo zone
	)
	{
		if (TradingHoursHelpers.Parse(hours, zone.Id) is not { } schedule)
			return [];
		var sessions = new List<(DateTimeOffset Start, DateTimeOffset End)>();
		foreach (
			var (start, length) in schedule
				.Days.SelectMany(day =>
					day.Value.Select(x =>
					{
						var wall = day.Key.ToDateTime(x.Start);
						return (new DateTimeOffset(wall, zone.GetUtcOffset(wall)), x.Length);
					})
				)
				.OrderBy(x => x.Item1)
		)
		{
			var end = start + length;
			if (sessions.Count > 0 && start <= sessions[^1].End)
				sessions[^1] = (
					sessions[^1].Start,
					end > sessions[^1].End ? end : sessions[^1].End
				);
			else
				sessions.Add((start, end));
		}
		return sessions;
	}

	// "16:00" today in the exchange's zone, "Mon 09:30" on another day
	private static string FormatPoint(DateTimeOffset point, TimeZoneInfo zone, DateOnly today)
	{
		var local = TimeZoneInfo.ConvertTime(point, zone).DateTime;
		return DateOnly.FromDateTime(local) == today
			? local.ToString("HH:mm", CultureInfo.InvariantCulture)
			: local.ToString("ddd HH:mm", CultureInfo.InvariantCulture);
	}
}
