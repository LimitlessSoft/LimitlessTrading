using LT.Contracts.Constants;
using LT.Contracts.Helpers;

namespace LT.App.Widgets.Settings;

/// <summary>
/// One entry of the time zone picker; <see cref="Id"/> null is the system's local zone (the
/// default).
/// </summary>
public sealed record TimeZoneOption(string? Id, string Label)
{
	public override string ToString() => Label;

	/// <summary>
	/// System local first, then the Exchange time shortcut (New York), then every zone of this
	/// computer. A saved zone missing from the list (settings from another OS) is kept as an
	/// entry of its own.
	/// </summary>
	public static List<TimeZoneOption> CreateAll(string? savedId)
	{
		var options = new List<TimeZoneOption>
		{
			new(null, $"System local — {TimeZoneInfo.Local.DisplayName}"),
		};
		if (
			TimeZoneInfo.TryFindSystemTimeZoneById(
				DisplayTimeConstants.ExchangeTimeZoneId,
				out var exchange
			)
		)
			options.Add(
				new(
					DisplayTimeConstants.ExchangeTimeZoneId,
					$"Exchange time — {exchange.DisplayName}"
				)
			);
		options.AddRange(
			TimeZoneInfo.GetSystemTimeZones().Select(x => new TimeZoneOption(x.Id, x.DisplayName))
		);
		if (
			!string.IsNullOrWhiteSpace(savedId)
			&& options.All(x => x.Id != savedId)
			&& DisplayTimeHelpers.IsValidZoneId(savedId)
		)
			options.Add(new(savedId, DisplayTimeHelpers.ResolveZone(savedId).DisplayName));
		return options;
	}
}
