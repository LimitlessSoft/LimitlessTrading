namespace LT.Contracts.Helpers;

/// <summary>
/// The display time zone: what every shown time is converted to. Business rules (trading days,
/// Flex statements) keep their own fixed zones and never go through here.
/// </summary>
public static class DisplayTimeHelpers
{
	/// <summary>
	/// The zone for a saved id; no id (the default) or one this computer doesn't know is the
	/// system's local zone.
	/// </summary>
	public static TimeZoneInfo ResolveZone(string? zoneId) =>
		!string.IsNullOrWhiteSpace(zoneId)
		&& TimeZoneInfo.TryFindSystemTimeZoneById(zoneId, out var zone)
			? zone
			: TimeZoneInfo.Local;

	/// <summary>No id (system local) or one this computer knows.</summary>
	public static bool IsValidZoneId(string? zoneId) =>
		string.IsNullOrWhiteSpace(zoneId) || TimeZoneInfo.TryFindSystemTimeZoneById(zoneId, out _);

	/// <summary><paramref name="time"/> as the clock in <paramref name="zone"/> shows it.</summary>
	public static DateTimeOffset ToZone(DateTimeOffset time, TimeZoneInfo zone) =>
		TimeZoneInfo.ConvertTime(time, zone);
}
