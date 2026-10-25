using LT.Contracts.Dtos.Updates;
using LT.Contracts.Helpers;

namespace LT.Tests.Contracts.Helpers;

public class UpdateHelpersTests
{
	[Theory]
	[InlineData("v0.2.0", "0.1.0", true)]
	[InlineData("0.1.1", "0.1.0", true)]
	[InlineData("v1.0.0", "0.9.9", true)]
	[InlineData("0.10.0", "0.9.0", true)]
	[InlineData("0.1.0", "0.1.0", false)]
	[InlineData("v0.1.0", "0.1.0+abc123", false)]
	[InlineData("0.1.0", "0.2.0", false)]
	[InlineData("0.2.0", "0.2.0-beta.1", true)]
	[InlineData("0.2.0-beta.1", "0.2.0", false)]
	[InlineData("0.2.0-beta.2", "0.2.0-beta.1", true)]
	[InlineData("0.2", "0.2.0", false)]
	[InlineData("v0.1.0", "0.0.0-dev", true)]
	[InlineData("nightly", "0.1.0", false)]
	public void IsNewer_ComparesSemanticVersions(string candidate, string current, bool expected) =>
		Assert.Equal(expected, UpdateHelpers.IsNewer(candidate, current));

	[Theory]
	[InlineData("v0.2.0", "0.2.0")]
	[InlineData(" V1.0.0+sha ", "1.0.0")]
	[InlineData("0.0.0-dev", "0.0.0-dev")]
	public void NormalizeVersion_DropsPrefixAndMetadata(string version, string expected) =>
		Assert.Equal(expected, UpdateHelpers.NormalizeVersion(version));

	[Fact]
	public void SelectAsset_PicksThisSystemsArchive()
	{
		AppReleaseAssetDto[] assets =
		[
			new() { Name = "LimitlessTrading-0.2.0-win-x64.zip" },
			new() { Name = "LimitlessTrading-0.2.0-linux-x64.tar.gz" },
		];

		Assert.Equal(
			"LimitlessTrading-0.2.0-linux-x64.tar.gz",
			UpdateHelpers.SelectAsset(assets, "-linux-x64.tar.gz")?.Name
		);
		Assert.Null(UpdateHelpers.SelectAsset(assets, "-osx-arm64.tar.gz"));
	}
}
