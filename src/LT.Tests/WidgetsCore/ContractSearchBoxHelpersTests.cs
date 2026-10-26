using LT.App.Helpers;

namespace LT.Tests.WidgetsCore;

/// <summary>
/// Esc in a ticker search box: an always-shown one (the watchlist's) lets an idle Esc through to
/// the workspace, so an unpinned docked watchlist collapses like every other widget.
/// </summary>
public class ContractSearchBoxHelpersTests
{
	[Theory]
	[InlineData(null)]
	[InlineData("")]
	public void An_idle_always_shown_box_lets_escape_through(string? text) =>
		Assert.False(ContractSearchBoxHelpers.ConsumesEscape(text, false, true));

	[Fact]
	public void Typed_text_is_cleared_first() =>
		Assert.True(ContractSearchBoxHelpers.ConsumesEscape("AAPL", false, true));

	[Fact]
	public void An_open_dropdown_is_closed_first() =>
		Assert.True(ContractSearchBoxHelpers.ConsumesEscape(string.Empty, true, true));

	[Fact]
	public void A_box_shown_only_for_an_edit_always_ends_it() =>
		Assert.True(ContractSearchBoxHelpers.ConsumesEscape(string.Empty, false, false));
}
