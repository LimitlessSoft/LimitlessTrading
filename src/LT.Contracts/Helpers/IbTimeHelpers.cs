using System.Globalization;

namespace LT.Contracts.Helpers;

/// <summary>Parses the time strings IB sends with formatDate=2.</summary>
public static class IbTimeHelpers
{
	/// <summary>
	/// Intraday bars come as Unix seconds ("1729700000"); daily and longer as "yyyyMMdd".
	/// Executions use "yyyyMMdd HH:mm:ss" optionally followed by a time zone id.
	/// </summary>
	public static DateTimeOffset? Parse(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
			return null;

		value = value.Trim();
		if (
			value.Length > 8
			&& long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var s)
		)
			return DateTimeOffset.FromUnixTimeSeconds(s);

		if (
			value.Length == 8
			&& DateTime.TryParseExact(
				value,
				"yyyyMMdd",
				CultureInfo.InvariantCulture,
				DateTimeStyles.None,
				out var date
			)
		)
			return new DateTimeOffset(date, TimeSpan.Zero);

		var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
		if (
			parts.Length >= 2
			&& DateTime.TryParseExact(
				$"{parts[0]} {parts[1]}",
				["yyyyMMdd HH:mm:ss", "yyyyMMdd-HH:mm:ss"],
				CultureInfo.InvariantCulture,
				DateTimeStyles.None,
				out var dateTime
			)
		)
		{
			var zone = parts.Length > 2 ? FindTimeZone(parts[2]) : TimeZoneInfo.Local;
			return new DateTimeOffset(dateTime, zone.GetUtcOffset(dateTime));
		}

		if (
			DateTime.TryParseExact(
				value,
				"yyyyMMdd-HH:mm:ss",
				CultureInfo.InvariantCulture,
				DateTimeStyles.AssumeUniversal,
				out var utc
			)
		)
			return new DateTimeOffset(utc.ToUniversalTime(), TimeSpan.Zero);

		return null;
	}

	public static TimeZoneInfo FindTimeZone(string id)
	{
		try
		{
			return TimeZoneInfo.FindSystemTimeZoneById(id);
		}
		catch (TimeZoneNotFoundException)
		{
			return TimeZoneInfo.Local;
		}
		catch (InvalidTimeZoneException)
		{
			return TimeZoneInfo.Local;
		}
	}
}
