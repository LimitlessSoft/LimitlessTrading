using LT.Contracts.Enums;
using LT.Contracts.Helpers;
using LT.Contracts.Requests.Gateway;

namespace LT.Tests.Contracts.Helpers;

public class IbcHelpersTests
{
	private static StartGatewayRequest Request(
		TradingMode mode = TradingMode.Paper,
		bool virtualDisplay = false
	) =>
		new()
		{
			Username = "trader1",
			Password = "s3cret",
			TradingMode = mode,
			IbcPath = "/opt/ibc",
			GatewayPath = "/home/u/Jts",
			GatewayVersion = "1037",
			UseVirtualDisplay = virtualDisplay,
		};

	[Theory]
	[InlineData(TradingMode.Live, 4001)]
	[InlineData(TradingMode.Paper, 4002)]
	public void GetApiPort_maps_mode_to_gateway_port(TradingMode mode, int expected) =>
		Assert.Equal(expected, IbcHelpers.GetApiPort(mode));

	[Fact]
	public void BuildConfigIni_has_credentials_mode_and_api_settings()
	{
		var lines = IbcHelpers.BuildConfigIni(Request(TradingMode.Live)).Split('\n');

		Assert.Contains("IbLoginId=trader1", lines);
		Assert.Contains("IbPassword=s3cret", lines);
		Assert.Contains("TradingMode=live", lines);
		Assert.Contains("OverrideTwsApiPort=4001", lines);
		Assert.Contains("ReadOnlyApi=no", lines);
		Assert.Contains("CommandServerPort=7462", lines);
	}

	[Fact]
	public void BuildLaunch_unix_runs_ibcstart_without_credentials_on_the_command_line()
	{
		var launch = IbcHelpers.BuildLaunch(Request(), "/cfg/ibc.ini", false, true);

		Assert.Equal("/bin/bash", launch.FileName);
		Assert.Equal(Path.Combine("/opt/ibc", "scripts", "ibcstart.sh"), launch.Arguments[0]);
		Assert.Contains("--gateway", launch.Arguments);
		Assert.Contains("--ibc-ini=/cfg/ibc.ini", launch.Arguments);
		Assert.Contains("--mode=paper", launch.Arguments);
		Assert.DoesNotContain(launch.Arguments, x => x.Contains("s3cret"));
		Assert.Equal("/cfg/ibc.ini", launch.ConfigFilePath);
	}

	[Fact]
	public void BuildLaunch_linux_virtual_display_wraps_in_xvfb_run()
	{
		var launch = IbcHelpers.BuildLaunch(Request(virtualDisplay: true), "/c.ini", false, true);

		Assert.Equal("xvfb-run", launch.FileName);
		Assert.Equal(["-a", "/bin/bash"], launch.Arguments.Take(2));
	}

	[Fact]
	public void BuildLaunch_virtual_display_is_ignored_outside_linux()
	{
		var launch = IbcHelpers.BuildLaunch(Request(virtualDisplay: true), "/c.ini", false, false);

		Assert.Equal("/bin/bash", launch.FileName);
	}

	[Fact]
	public void BuildLaunch_windows_uses_start_ibc_bat()
	{
		var launch = IbcHelpers.BuildLaunch(Request(), @"C:\cfg\ibc.ini", true, false);

		Assert.Equal("cmd.exe", launch.FileName);
		Assert.EndsWith("StartIBC.bat", launch.Arguments[1]);
		Assert.Contains("/Gateway", launch.Arguments);
		Assert.Contains(@"/IbcIni:C:\cfg\ibc.ini", launch.Arguments);
		Assert.DoesNotContain(launch.Arguments, x => x.Contains("s3cret"));
	}

	[Theory]
	[InlineData(null, IbcOutputEvent.None)]
	[InlineData("IBC: Starting session", IbcOutputEvent.None)]
	[InlineData(
		"IBC: detected dialog entitled: Second Factor Authentication; event=Opened",
		IbcOutputEvent.SecondFactorRequested
	)]
	[InlineData("IBC: Login has completed", IbcOutputEvent.LoginCompleted)]
	[InlineData("Unrecognized Username or Password", IbcOutputEvent.LoginFailed)]
	public void ParseOutputLine_recognises_ibc_events(string? line, IbcOutputEvent expected) =>
		Assert.Equal(expected, IbcHelpers.ParseOutputLine(line));
}
