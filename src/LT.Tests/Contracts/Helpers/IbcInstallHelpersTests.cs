using LT.Contracts.Dtos.Gateway;
using LT.Contracts.Helpers;
using LT.Tests.Fakes;

namespace LT.Tests.Contracts.Helpers;

public class IbcInstallHelpersTests
{
	private const string Home = "/home/u";

	private static readonly IReadOnlyList<string> IbcCandidates =
		IbcInstallHelpers.GetIbcCandidates(Home, "", "", isWindows: false);

	private static readonly IReadOnlyList<string> SearchRoots = IbcInstallHelpers.GetIbcSearchRoots(
		Home,
		isWindows: false
	);

	[Theory]
	[InlineData("/opt/ibc")]
	[InlineData("/opt/ibc/")]
	[InlineData("/opt/ibc/scripts")]
	[InlineData("/opt/ibc/scripts/ibcstart.sh")]
	public void ResolveIbcPath_accepts_the_folder_its_scripts_folder_or_the_script(string selected)
	{
		var probe = new FakeIbInstallProbe().AddFile("/opt/ibc/scripts/ibcstart.sh");

		Assert.Equal("/opt/ibc", IbcInstallHelpers.ResolveIbcPath(probe, selected, false));
	}

	[Fact]
	public void ResolveIbcPath_without_a_start_script_is_null() =>
		Assert.Null(
			IbcInstallHelpers.ResolveIbcPath(
				new FakeIbInstallProbe().AddFile("/opt/ibc/IBC.jar"),
				"/opt/ibc",
				false
			)
		);

	[Fact]
	public void FindIbcPath_prefers_the_standard_locations()
	{
		var probe = new FakeIbInstallProbe()
			.AddFile("/home/u/Downloads/IBCLinux-3.20.0/scripts/ibcstart.sh")
			.AddFile("/opt/ibc/scripts/ibcstart.sh");

		Assert.Equal(
			"/opt/ibc",
			IbcInstallHelpers.FindIbcPath(probe, IbcCandidates, SearchRoots, false)
		);
	}

	[Fact]
	public void FindIbcPath_finds_an_unzipped_download()
	{
		var probe = new FakeIbInstallProbe().AddFile(
			"/home/u/Downloads/IBCLinux-3.20.0/scripts/ibcstart.sh"
		);

		Assert.Equal(
			"/home/u/Downloads/IBCLinux-3.20.0",
			IbcInstallHelpers.FindIbcPath(probe, IbcCandidates, SearchRoots, false)
		);
	}

	[Fact]
	public void FindIbcPath_returns_null_when_nothing_is_installed() =>
		Assert.Null(
			IbcInstallHelpers.FindIbcPath(
				new FakeIbInstallProbe().AddFile("/home/u/Jts/jars/twslaunch.jar"),
				IbcCandidates,
				SearchRoots,
				false
			)
		);

	[Theory]
	[InlineData("/home/u/Jts")]
	[InlineData("/home/u/Jts/ibgateway")]
	[InlineData("/home/u/Jts/ibgateway/1030")]
	public void ResolveGatewayPath_finds_the_root_and_versions_newest_first(string selected)
	{
		var probe = new FakeIbInstallProbe()
			.AddFile("/home/u/Jts/ibgateway/1030/ibgateway")
			.AddFile("/home/u/Jts/ibgateway/1037/ibgateway");

		var found = IbcInstallHelpers.ResolveGatewayPath(probe, selected);

		Assert.Equal("/home/u/Jts", found?.GatewayPath);
		Assert.Equal(["1037", "1030"], found!.GatewayVersions);
	}

	[Fact]
	public void ResolveGatewayPath_reads_macos_app_folders()
	{
		var probe = new FakeIbInstallProbe().AddFile(
			"/home/u/Applications/IB Gateway 10.37/Contents/Info.plist"
		);

		var found = IbcInstallHelpers.ResolveGatewayPath(probe, "/home/u/Applications");

		Assert.Equal(["1037"], found!.GatewayVersions);
	}

	[Fact]
	public void ResolveGatewayPath_with_only_tws_is_null() =>
		Assert.Null(
			IbcInstallHelpers.ResolveGatewayPath(
				new FakeIbInstallProbe().AddFile("/home/u/Jts/jars/twslaunch.jar"),
				"/home/u/Jts"
			)
		);

	[Theory]
	[InlineData(false, false, "IBCLinux-3.24.2.zip")]
	[InlineData(false, true, "IBCMacos-3.24.2.zip")]
	[InlineData(true, false, "IBCWin-3.24.2.zip")]
	public void SelectReleaseAsset_picks_the_zip_for_the_os(
		bool isWindows,
		bool isMacOS,
		string expected
	)
	{
		IReadOnlyList<IbcReleaseAssetDto> assets =
		[
			new() { Name = "IBCLinux-3.24.2.zip" },
			new() { Name = "IBCMacos-3.24.2.zip" },
			new() { Name = "IBCWin-3.24.2.zip" },
		];

		Assert.Equal(
			expected,
			IbcInstallHelpers.SelectReleaseAsset(assets, isWindows, isMacOS)?.Name
		);
	}
}
