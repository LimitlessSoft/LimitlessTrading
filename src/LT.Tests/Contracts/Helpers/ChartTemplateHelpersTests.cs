using LT.Contracts.Constants;
using LT.Contracts.Helpers;

namespace LT.Tests.Contracts.Helpers;

public class ChartTemplateHelpersTests
{
	[Fact]
	public void A_free_name_is_kept_as_it_is() =>
		Assert.Equal("Swing", ChartTemplateHelpers.GetFreeName(" Swing ", ["Scalping"]));

	[Fact]
	public void A_taken_name_gets_the_first_free_number() =>
		Assert.Equal(
			"Swing (3)",
			ChartTemplateHelpers.GetFreeName("Swing", ["swing", "Swing (2)", "Swing (4)"])
		);

	[Fact]
	public void A_numbered_name_stays_within_the_length_limit()
	{
		var name = new string('x', ChartTemplateConstants.NameMaxLength);

		var free = ChartTemplateHelpers.GetFreeName(name, [name]);

		Assert.Equal(ChartTemplateConstants.NameMaxLength, free.Length);
		Assert.EndsWith(" (2)", free);
	}

	[Theory]
	[InlineData("Scalping", "Scalping.json")]
	[InlineData("1m / 5m: EMA", "1m _ 5m_ EMA.json")]
	[InlineData("   ", "chart-templates.json")]
	public void File_names_drop_characters_file_systems_refuse(string name, string expected) =>
		Assert.Equal(expected, ChartTemplateHelpers.GetFileName(name));
}
