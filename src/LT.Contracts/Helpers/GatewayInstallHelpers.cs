using System.Text.RegularExpressions;
using LT.Contracts.Dtos.Gateway;
using LT.Contracts.Interfaces.IProcesses;

namespace LT.Contracts.Helpers;

/// <summary>
/// Finds IB Gateway installs on disk, makes sense of a folder the user picked and finds the
/// launcher of one installed version.
/// </summary>
public static partial class GatewayInstallHelpers
{
	/// <summary>Gateway's install defaults: Jts (Windows, Linux), Applications (macOS).</summary>
	public static IReadOnlyList<string> GetGatewayCandidates(string home, bool isWindows) =>
		isWindows
			? [@"C:\Jts", Path.Combine(home, "Jts")]
			: [Path.Combine(home, "Jts"), Path.Combine(home, "Applications"), "/Applications"];

	/// <summary>
	/// Gateway major versions under an install root, newest first: ibgateway/1037 (Windows,
	/// Linux) or "IB Gateway 10.37" (macOS).
	/// </summary>
	public static IReadOnlyList<string> GetGatewayVersions(IIbInstallProbe probe, string root)
	{
		var versions = probe
			.GetDirectoryNames(Path.Combine(root, "ibgateway"))
			.Where(x => VersionRegex().IsMatch(x))
			.ToList();
		versions.AddRange(
			probe
				.GetDirectoryNames(root)
				.Select(x => MacAppRegex().Match(x))
				.Where(x => x.Success)
				.Select(x => x.Groups[1].Value + x.Groups[2].Value)
		);
		return versions.Distinct().OrderByDescending(int.Parse).ToList();
	}

	/// <summary>
	/// Accepts the Gateway root (Jts, Applications), its ibgateway folder or one version folder.
	/// Null when no installed version is found there.
	/// </summary>
	public static GatewayInstallDto? ResolveGatewayPath(IIbInstallProbe probe, string selected)
	{
		if (string.IsNullOrWhiteSpace(selected))
			return null;
		var path = Path.TrimEndingDirectorySeparator(selected.Trim());
		var name = Path.GetFileName(path);
		var parent = Path.GetDirectoryName(path);

		if (
			parent != null
			&& VersionRegex().IsMatch(name)
			&& Path.GetFileName(parent) == "ibgateway"
		)
			path = Path.GetDirectoryName(parent) ?? path;
		else if (parent != null && (name == "ibgateway" || MacAppRegex().IsMatch(name)))
			path = parent;

		var versions = GetGatewayVersions(probe, path);
		return versions.Count == 0
			? null
			: new GatewayInstallDto { GatewayPath = path, GatewayVersions = versions };
	}

	public static GatewayInstallDto? FindGateway(
		IIbInstallProbe probe,
		IReadOnlyList<string> candidates
	)
	{
		foreach (var candidate in candidates)
			if (ResolveGatewayPath(probe, candidate) is { } found)
				return found;
		return null;
	}

	/// <summary>
	/// Launcher file names in a Windows / Linux version folder. The Linux offline installer names
	/// it ibgateway or ibgateway1 (10.50 seen with the latter).
	/// </summary>
	public static IReadOnlyList<string> GetLauncherNames(bool isWindows) =>
		isWindows ? ["ibgateway.exe"] : ["ibgateway", "ibgateway1"];

	/// <summary>
	/// The executable that starts <paramref name="version"/>: ibgateway/&lt;version&gt;/ibgateway
	/// (Windows: ibgateway.exe), or on macOS the binary in "IB Gateway 10.37/*.app/Contents/MacOS".
	/// Null when that version has no launcher there.
	/// </summary>
	public static string? FindLauncher(
		IIbInstallProbe probe,
		string gatewayPath,
		string version,
		bool isWindows
	)
	{
		var versionFolder = Path.Combine(gatewayPath, "ibgateway", version);
		foreach (var name in GetLauncherNames(isWindows))
		{
			var launcher = Path.Combine(versionFolder, name);
			if (probe.FileExists(launcher))
				return launcher;
		}

		foreach (var folder in probe.GetDirectoryNames(gatewayPath))
		{
			var match = MacAppRegex().Match(folder);
			if (!match.Success || match.Groups[1].Value + match.Groups[2].Value != version)
				continue;
			var appFolder = Path.Combine(gatewayPath, folder);
			foreach (
				var app in probe
					.GetDirectoryNames(appFolder)
					.Where(x => x.EndsWith(".app", StringComparison.OrdinalIgnoreCase))
					.Order()
			)
			{
				var macOs = Path.Combine(appFolder, app, "Contents", "MacOS");
				var files = probe.GetFileNames(macOs);
				// The bundle's executable is named after the app, or install4j's stub
				var executable =
					files.FirstOrDefault(x => x == Path.GetFileNameWithoutExtension(app))
					?? files.FirstOrDefault(x => x == "JavaApplicationStub")
					?? files.Order().FirstOrDefault();
				if (executable != null)
					return Path.Combine(macOs, executable);
			}
		}
		return null;
	}

	[GeneratedRegex(@"^\d{3,4}$")]
	private static partial Regex VersionRegex();

	[GeneratedRegex(@"^IB Gateway (\d+)\.(\d+)$")]
	private static partial Regex MacAppRegex();
}
