using LT.Contracts.Helpers;
using LT.Tests.Fakes;

namespace LT.Tests.Contracts.Helpers;

public class GatewayInstallHelpersTests
{
	[Theory]
	[InlineData("/home/u/Jts")]
	[InlineData("/home/u/Jts/ibgateway")]
	[InlineData("/home/u/Jts/ibgateway/1030")]
	public void ResolveGatewayPath_finds_the_root_and_versions_newest_first(string selected)
	{
		var probe = new FakeIbInstallProbe()
			.AddFile("/home/u/Jts/ibgateway/1030/ibgateway")
			.AddFile("/home/u/Jts/ibgateway/1037/ibgateway");

		var found = GatewayInstallHelpers.ResolveGatewayPath(probe, selected);

		Assert.Equal("/home/u/Jts", found?.GatewayPath);
		Assert.Equal(["1037", "1030"], found!.GatewayVersions);
	}

	[Fact]
	public void ResolveGatewayPath_reads_macos_app_folders()
	{
		var probe = new FakeIbInstallProbe().AddFile(
			"/home/u/Applications/IB Gateway 10.37/Contents/Info.plist"
		);

		var found = GatewayInstallHelpers.ResolveGatewayPath(probe, "/home/u/Applications");

		Assert.Equal(["1037"], found!.GatewayVersions);
	}

	[Fact]
	public void ResolveGatewayPath_with_only_tws_is_null() =>
		Assert.Null(
			GatewayInstallHelpers.ResolveGatewayPath(
				new FakeIbInstallProbe().AddFile("/home/u/Jts/jars/twslaunch.jar"),
				"/home/u/Jts"
			)
		);

	[Theory]
	[InlineData("ibgateway")]
	[InlineData("ibgateway1")]
	public void FindLauncher_finds_the_linux_launcher(string name)
	{
		var probe = new FakeIbInstallProbe()
			.AddFile("/home/u/Jts/ibgateway/1050/ibgateway.vmoptions")
			.AddFile($"/home/u/Jts/ibgateway/1050/{name}");

		Assert.Equal(
			$"/home/u/Jts/ibgateway/1050/{name}",
			GatewayInstallHelpers.FindLauncher(probe, "/home/u/Jts", "1050", isWindows: false)
		);
	}

	[Fact]
	public void FindLauncher_finds_the_windows_exe()
	{
		var probe = new FakeIbInstallProbe().AddFile(@"C:\Jts\ibgateway\1037\ibgateway.exe");

		Assert.EndsWith(
			"ibgateway.exe",
			GatewayInstallHelpers.FindLauncher(probe, @"C:\Jts", "1037", isWindows: true)
		);
	}

	[Fact]
	public void FindLauncher_finds_the_macos_app_executable()
	{
		var probe = new FakeIbInstallProbe()
			.AddFile("/u/Applications/IB Gateway 10.37/IB Gateway 10.37.app/Contents/Info.plist")
			.AddFile(
				"/u/Applications/IB Gateway 10.37/IB Gateway 10.37.app/Contents/MacOS/JavaApplicationStub"
			);

		Assert.Equal(
			"/u/Applications/IB Gateway 10.37/IB Gateway 10.37.app/Contents/MacOS/JavaApplicationStub",
			GatewayInstallHelpers.FindLauncher(probe, "/u/Applications", "1037", isWindows: false)
		);
	}

	[Fact]
	public void FindLauncher_for_a_version_that_is_not_installed_is_null() =>
		Assert.Null(
			GatewayInstallHelpers.FindLauncher(
				new FakeIbInstallProbe().AddFile("/home/u/Jts/ibgateway/1037/ibgateway"),
				"/home/u/Jts",
				"1050",
				isWindows: false
			)
		);
}
