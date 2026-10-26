using LT.Contracts.Helpers;

namespace LT.Tests.Contracts.Helpers;

public class SettingsSearchHelpersTests
{
	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void An_empty_query_matches_everything(string? query) =>
		Assert.True(SettingsSearchHelpers.Matches(query, "Heartbeat interval"));

	[Theory]
	[InlineData("heart")]
	[InlineData("INTERVAL heartbeat")]
	[InlineData("seconds heart")]
	public void Every_word_must_appear_in_any_text_in_any_order(string query) =>
		Assert.True(SettingsSearchHelpers.Matches(query, "Heartbeat interval", "In seconds", null));

	[Fact]
	public void A_word_found_nowhere_fails_the_match() =>
		Assert.False(SettingsSearchHelpers.Matches("heartbeat gateway", "Heartbeat interval"));
}
