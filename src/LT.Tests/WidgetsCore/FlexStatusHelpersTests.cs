using LT.App.Helpers;
using LT.Contracts.Dtos.Flex;

namespace LT.Tests.WidgetsCore;

public class FlexStatusHelpersTests
{
	[Fact]
	public void Without_flex_the_banner_asks_for_the_setup()
	{
		var banner = FlexStatusHelpers.GetBanner(new FlexImportStatusDto { IsConfigured = false });

		Assert.NotNull(banner);
		Assert.False(banner.IsWarning);
		Assert.Equal("History needs IB Flex — set it up once in Settings.", banner.Text);
	}

	[Fact]
	public void An_error_only_the_user_can_fix_shows_as_a_warning_with_its_text()
	{
		var banner = FlexStatusHelpers.GetBanner(
			new FlexImportStatusDto
			{
				IsConfigured = true,
				LastError = "The Flex token has expired.",
				ErrorNeedsUser = true,
			}
		);

		Assert.NotNull(banner);
		Assert.True(banner.IsWarning);
		Assert.Contains("The Flex token has expired.", banner.Text);
	}

	[Fact]
	public void A_passing_error_shows_no_banner()
	{
		var status = new FlexImportStatusDto
		{
			IsConfigured = true,
			LastError = "The server is busy.",
			ErrorNeedsUser = false,
		};

		Assert.Null(FlexStatusHelpers.GetBanner(status));
	}

	[Fact]
	public void A_working_setup_shows_no_banner() =>
		Assert.Null(
			FlexStatusHelpers.GetBanner(
				new FlexImportStatusDto
				{
					IsConfigured = true,
					LastActivityImport = DateTimeOffset.UnixEpoch,
				}
			)
		);

	[Fact]
	public void Import_times_are_in_the_display_zone()
	{
		var tokyo = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tokyo");
		var import = new DateTimeOffset(2026, 10, 26, 16, 0, 0, TimeSpan.Zero); // 01:00 in Tokyo
		var status = new FlexImportStatusDto
		{
			IsConfigured = true,
			HasTradeConfirmQuery = true,
			LastActivityImport = import,
			LastTradeConfirmImport = import,
		};

		Assert.Equal(
			"daily import 01:00 · today's trades 01:00",
			FlexStatusHelpers.Describe(status, tokyo, import.AddHours(1))
		);
		Assert.Contains(
			"(last import 01:00)",
			FlexStatusHelpers.DescribeTodayTrades(status, tokyo, import.AddHours(1))
		);
	}

	[Fact]
	public void The_cash_movements_hint_says_which_query_sections_to_add()
	{
		var status = new FlexImportStatusDto
		{
			IsConfigured = true,
			BackfillComplete = true,
			HasCashTransactionsSection = false,
			HasTransfersSection = false,
		};

		Assert.Equal(
			"Add Cash Transactions (option Deposits & Withdrawals) and Transfers to your Activity Flex query in Client Portal, then press F5.",
			FlexStatusHelpers.GetCashMovementsHint(status, hasMovements: false)
		);
		status.HasCashTransactionsSection = true;
		Assert.Equal(
			"Add Transfers to your Activity Flex query in Client Portal, then press F5.",
			FlexStatusHelpers.GetCashMovementsHint(status, hasMovements: true)
		);
	}

	[Fact]
	public void The_cash_movements_hint_never_leaves_an_empty_overlay_unexplained()
	{
		var status = new FlexImportStatusDto
		{
			IsConfigured = true,
			BackfillComplete = true,
			HasCashTransactionsSection = true,
			HasTransfersSection = true,
		};

		Assert.Null(FlexStatusHelpers.GetCashMovementsHint(status, hasMovements: true));
		Assert.Equal(
			"No deposits, withdrawals or transfers in the imported history.",
			FlexStatusHelpers.GetCashMovementsHint(status, hasMovements: false)
		);
		status.BackfillComplete = false;
		Assert.Equal(
			"Importing deposits and transfers…",
			FlexStatusHelpers.GetCashMovementsHint(status, hasMovements: false)
		);
		Assert.Equal(
			"Waiting for the IB Flex import.",
			FlexStatusHelpers.GetCashMovementsHint(
				new FlexImportStatusDto { IsConfigured = true },
				hasMovements: false
			)
		);
		// A state from before the sections were tracked, while that import runs
		Assert.Equal(
			"Importing deposits and transfers…",
			FlexStatusHelpers.GetCashMovementsHint(
				new FlexImportStatusDto
				{
					IsConfigured = true,
					Activity = "Importing account history 2026-10-18 – 2026-10-25",
				},
				hasMovements: false
			)
		);
		Assert.NotNull(
			FlexStatusHelpers.GetCashMovementsHint(new FlexImportStatusDto(), hasMovements: false)
		);
	}
}
