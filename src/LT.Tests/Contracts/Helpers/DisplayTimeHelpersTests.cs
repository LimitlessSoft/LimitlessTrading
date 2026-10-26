using LT.Contracts.Constants;
using LT.Contracts.Helpers;

namespace LT.Tests.Contracts.Helpers;

public class DisplayTimeHelpersTests
{
	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("Not/AZone")]
	public void No_or_an_unknown_zone_is_the_system_local_zone(string? zoneId) =>
		Assert.Equal(TimeZoneInfo.Local, DisplayTimeHelpers.ResolveZone(zoneId));

	[Fact]
	public void A_known_zone_resolves_to_itself() =>
		Assert.Equal(
			TimeSpan.FromHours(9),
			DisplayTimeHelpers.ResolveZone("Asia/Tokyo").BaseUtcOffset
		);

	[Theory]
	[InlineData(null, true)]
	[InlineData("", true)]
	[InlineData(DisplayTimeConstants.ExchangeTimeZoneId, true)]
	[InlineData("Not/AZone", false)]
	public void Only_no_zone_or_a_known_one_is_valid(string? zoneId, bool valid) =>
		Assert.Equal(valid, DisplayTimeHelpers.IsValidZoneId(zoneId));

	[Fact]
	public void A_time_is_shown_as_the_clock_in_the_zone_reads_it()
	{
		var newYork = DisplayTimeHelpers.ResolveZone(DisplayTimeConstants.ExchangeTimeZoneId);

		// 26 Oct 2026 is still daylight saving time in New York (UTC-4)
		var shown = DisplayTimeHelpers.ToZone(
			new DateTimeOffset(2026, 10, 26, 14, 0, 0, TimeSpan.Zero),
			newYork
		);

		Assert.Equal(new DateTime(2026, 10, 26, 10, 0, 0), shown.DateTime);
		Assert.Equal(TimeSpan.FromHours(-4), shown.Offset);
	}
}
