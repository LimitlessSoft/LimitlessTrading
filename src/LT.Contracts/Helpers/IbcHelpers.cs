using LT.Contracts.Constants;
using LT.Contracts.Dtos.Gateway;
using LT.Contracts.Enums;
using LT.Contracts.Requests.Gateway;

namespace LT.Contracts.Helpers;

/// <summary>Builds the IBC launch (config + command line) and reads its console output.</summary>
public static class IbcHelpers
{
	/// <summary>IBC's own install defaults.</summary>
	public static string GetDefaultIbcPath() =>
		OperatingSystem.IsWindows() ? @"C:\IBC"
		: OperatingSystem.IsMacOS() ? Path.Combine(GetHome(), "ibc")
		: "/opt/ibc";

	/// <summary>Gateway's own install defaults (IBC's tws-path).</summary>
	public static string GetDefaultGatewayPath() =>
		OperatingSystem.IsWindows() ? @"C:\Jts"
		: OperatingSystem.IsMacOS() ? Path.Combine(GetHome(), "Applications")
		: Path.Combine(GetHome(), "Jts");

	public static int GetApiPort(TradingMode mode) =>
		mode == TradingMode.Live
			? ConnectionConstants.GatewayLivePort
			: ConnectionConstants.GatewayPaperPort;

	public static string GetIbcScriptPath(string ibcPath, bool isWindows) =>
		Path.Combine(ibcPath, "scripts", IbcInstallHelpers.GetIbcScriptName(isWindows));

	/// <summary>
	/// IBC config.ini. Credentials go here rather than on the command line, where any local user
	/// could read them from the process list.
	/// </summary>
	public static string BuildConfigIni(StartGatewayRequest request) =>
		string.Join(
			'\n',
			[
				$"IbLoginId={request.Username}",
				$"IbPassword={request.Password}",
				$"TradingMode={FormatMode(request.TradingMode)}",
				"FIX=no",
				$"OverrideTwsApiPort={GetApiPort(request.TradingMode)}",
				"ReadOnlyApi=no",
				// Localhost is trusted by default; anything else asking to connect is refused
				"AcceptIncomingConnectionAction=reject",
				"AcceptNonBrokerageAccountWarning=yes",
				// Starting from our app means "this session wins" over an older one
				"ExistingSessionDetectedAction=primary",
				"MinimizeMainWindow=yes",
				$"CommandServerPort={GatewayConstants.IbcCommandServerPort}",
				$"BindAddress={ConnectionConstants.DefaultHost}",
				"ControlFrom=",
				"",
			]
		);

	public static IbcLaunchDto BuildLaunch(
		StartGatewayRequest request,
		string configFilePath,
		bool isWindows,
		bool isLinux
	)
	{
		var script = GetIbcScriptPath(request.IbcPath, isWindows);
		var mode = FormatMode(request.TradingMode);
		var content = BuildConfigIni(request);

		if (isWindows)
			return new IbcLaunchDto
			{
				FileName = "cmd.exe",
				Arguments =
				[
					"/c",
					script,
					request.GatewayVersion,
					"/Gateway",
					$"/TwsPath:{request.GatewayPath}",
					$"/IbcPath:{request.IbcPath}",
					$"/IbcIni:{configFilePath}",
					$"/Mode:{mode}",
					"/On2FATimeout:exit",
				],
				ConfigFilePath = configFilePath,
				ConfigContent = content,
			};

		List<string> ibcArguments =
		[
			script,
			request.GatewayVersion,
			"--gateway",
			$"--tws-path={request.GatewayPath}",
			$"--ibc-path={request.IbcPath}",
			$"--ibc-ini={configFilePath}",
			$"--mode={mode}",
			"--on2fatimeout=exit",
		];
		var virtualDisplay = isLinux && request.UseVirtualDisplay;
		return new IbcLaunchDto
		{
			FileName = virtualDisplay ? "xvfb-run" : "/bin/bash",
			Arguments = virtualDisplay ? ["-a", "/bin/bash", .. ibcArguments] : ibcArguments,
			ConfigFilePath = configFilePath,
			ConfigContent = content,
		};
	}

	/// <summary>
	/// Best effort: matches IBC log lines and Gateway dialog titles that IBC reports. Readiness
	/// itself is decided by the API port opening, not by this.
	/// </summary>
	public static IbcOutputEvent ParseOutputLine(string? line)
	{
		if (string.IsNullOrWhiteSpace(line))
			return IbcOutputEvent.None;
		if (Contains(line, "Second Factor Authentication"))
			return IbcOutputEvent.SecondFactorRequested;
		if (Contains(line, "Login has completed"))
			return IbcOutputEvent.LoginCompleted;
		if (Contains(line, "Unrecognized Username or Password") || Contains(line, "Login failed"))
			return IbcOutputEvent.LoginFailed;
		return IbcOutputEvent.None;
	}

	private static string FormatMode(TradingMode mode) =>
		mode == TradingMode.Live ? "live" : "paper";

	private static bool Contains(string line, string value) =>
		line.Contains(value, StringComparison.OrdinalIgnoreCase);

	private static string GetHome() =>
		Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
}
