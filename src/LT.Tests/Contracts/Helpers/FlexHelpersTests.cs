using LT.Contracts.Enums;
using LT.Contracts.Helpers;

namespace LT.Tests.Contracts.Helpers;

public class FlexHelpersTests
{
	private static readonly TimeZoneInfo NewYork = FlexHelpers.GetStatementTimeZone();

	[Theory]
	[InlineData(1000, "CASH RECEIPTS / ELECTRONIC FUND TRANSFERS", CashMovementKind.Deposit)]
	[InlineData(-250, "DISBURSEMENT INITIATED BY FAKE NAME", CashMovementKind.Withdrawal)]
	[InlineData(300, "TRANSFER FROM U0000002", CashMovementKind.TransferIn)]
	[InlineData(-300, "Transfer to U0000002", CashMovementKind.TransferOut)]
	[InlineData(-50, "INTERNAL TRANSFER", CashMovementKind.TransferOut)]
	[InlineData(75, "ACATS cash in", CashMovementKind.TransferIn)]
	[InlineData(10, null, CashMovementKind.Deposit)]
	public void Tells_deposits_and_withdrawals_from_transfers_between_accounts(
		double amount,
		string? description,
		CashMovementKind expected
	) => Assert.Equal(expected, FlexHelpers.GetCashTransactionKind(amount, description));

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

	[Theory]
	[InlineData(" 1234567890 1234567890\n", "12345678901234567890")]
	[InlineData("\t123456789012\r\n", "123456789012")]
	public void A_pasted_token_loses_its_whitespace(string pasted, string expected) =>
		Assert.Equal(expected, FlexHelpers.NormalizeToken(pasted));

	[Theory]
	[InlineData("123456", "123456")]
	[InlineData("  123456 ", "123456")]
	[InlineData("Query ID: 123456", "123456")]
	[InlineData(
		"https://ndcdyn.interactivebrokers.com/AccountManagement/FlexWebService/SendRequest?t=11112222333344445555&q=987654&v=3",
		"987654"
	)]
	[InlineData("https://example.com/flex?queryId=4321&x=99999999", "4321")]
	[InlineData("LimitlessTrading activity (ID 765432)", "765432")]
	[InlineData("abc", "abc")]
	[InlineData("", "")]
	public void The_query_id_is_pulled_out_of_what_was_pasted(string pasted, string expected) =>
		Assert.Equal(expected, FlexHelpers.ExtractQueryId(pasted));

	[Theory]
	[InlineData(1012, true)]
	[InlineData(1015, true)]
	[InlineData(1013, true)]
	[InlineData(1014, true)]
	[InlineData(1019, false)]
	[InlineData(1018, false)]
	[InlineData(null, false)]
	public void Token_query_and_ip_errors_need_the_user(int? code, bool expected) =>
		Assert.Equal(expected, FlexHelpers.NeedsUserAction(code));
}
