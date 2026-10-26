using LT.Contracts.Helpers;

namespace LT.Tests.Contracts.Helpers;

public class FlexHelpersTests
{
	private static readonly TimeZoneInfo NewYork = FlexHelpers.GetStatementTimeZone();

	[Theory]
	[InlineData("20261026")]
	[InlineData("2026-10-26")]
	[InlineData("10/26/2026")]
	[InlineData("10/26/26")]
	[InlineData("26-Oct-26")]
	[InlineData("26-Oct-2026")]
	public void Reads_ibs_date_formats(string value) =>
		Assert.Equal(new DateOnly(2026, 10, 26), FlexHelpers.ParseDate(value));

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("tomorrow")]
	public void Unknown_dates_are_null(string? value) => Assert.Null(FlexHelpers.ParseDate(value));

	[Theory]
	[InlineData("20261026;093512")]
	[InlineData("20261026,093512")]
	[InlineData("20261026 09:35:12")]
	[InlineData("2026-10-26, 09:35:12")]
	[InlineData("2026-10-26T09:35:12")]
	[InlineData("20261026093512")]
	[InlineData("20261026;093512 EDT")]
	public void Reads_date_times_in_new_york_time(string value)
	{
		var (date, time) = FlexHelpers.ParseDateTime(value, NewYork);

		Assert.Equal(new DateOnly(2026, 10, 26), date);
		Assert.Equal(new DateTimeOffset(2026, 10, 26, 13, 35, 12, TimeSpan.Zero), time);
	}

	[Fact]
	public void Winter_times_are_five_hours_behind_utc()
	{
		var (_, time) = FlexHelpers.ParseDateTime("20260115;093000", NewYork);

		Assert.Equal(new DateTimeOffset(2026, 1, 15, 14, 30, 0, TimeSpan.Zero), time);
	}

	[Fact]
	public void An_explicit_offset_wins_over_the_zone()
	{
		var (_, time) = FlexHelpers.ParseDateTime("20261026;093512 +02:00", NewYork);

		Assert.Equal(new DateTimeOffset(2026, 10, 26, 7, 35, 12, TimeSpan.Zero), time);
	}

	[Fact]
	public void A_date_without_time_has_no_moment()
	{
		var (date, time) = FlexHelpers.ParseDateTime("20261026", NewYork);

		Assert.Equal(new DateOnly(2026, 10, 26), date);
		Assert.Null(time);
	}

	[Theory]
	[InlineData(1012, "expired")]
	[InlineData(1015, "invalid")]
	[InlineData(1013, "IP")]
	[InlineData(1014, "query ID")]
	[InlineData(1018, "Too many")]
	public void Error_codes_have_clear_messages(int code, string expected)
	{
		var message = FlexHelpers.GetErrorMessage(code, "IB text");

		Assert.Contains(expected, message);
		Assert.Contains(code.ToString(), message);
	}

	[Fact]
	public void Unknown_codes_keep_ibs_message() =>
		Assert.Equal("IB Flex error 1099: Odd", FlexHelpers.GetErrorMessage(1099, "Odd"));

	[Theory]
	[InlineData(1019, true)]
	[InlineData(1018, true)]
	[InlineData(1001, true)]
	[InlineData(1012, false)]
	[InlineData(1003, false)]
	public void Only_not_ready_codes_are_retried(int code, bool retryable) =>
		Assert.Equal(retryable, FlexHelpers.IsRetryable(code));

	[Fact]
	public void Request_dates_are_yyyymmdd() =>
		Assert.Equal("20260105", FlexHelpers.FormatRequestDate(new DateOnly(2026, 1, 5)));
}
