using LT.Contracts.Helpers;

namespace LT.Tests.Contracts.Helpers;

public class IbTimeHelpersTests
{
	[Fact]
	public void Unix_seconds_are_parsed() =>
		Assert.Equal(
			DateTimeOffset.FromUnixTimeSeconds(1761226200),
			IbTimeHelpers.Parse("1761226200")
		);

	[Fact]
	public void Daily_date_is_parsed() =>
		Assert.Equal(
			new DateTimeOffset(2026, 10, 23, 0, 0, 0, TimeSpan.Zero),
			IbTimeHelpers.Parse("20261023")
		);

	[Fact]
	public void Execution_time_with_zone_is_parsed()
	{
		var parsed = IbTimeHelpers.Parse("20261023 09:45:12 America/New_York");

		Assert.Equal(
			new DateTimeOffset(2026, 10, 23, 13, 45, 12, TimeSpan.Zero),
			parsed?.ToUniversalTime()
		);
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("garbage")]
	public void Unparseable_values_are_null(string? value) =>
		Assert.Null(IbTimeHelpers.Parse(value));
}
