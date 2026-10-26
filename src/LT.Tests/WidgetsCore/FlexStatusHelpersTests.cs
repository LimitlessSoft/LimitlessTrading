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
}
