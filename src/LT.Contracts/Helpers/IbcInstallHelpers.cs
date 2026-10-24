using System.Text.RegularExpressions;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Gateway;
using LT.Contracts.Interfaces.IProcesses;

namespace LT.Contracts.Helpers;

/// <summary>Finds IBC and IB Gateway installs on disk and makes sense of a folder the user picked.</summary>
public static partial class IbcInstallHelpers
{
	/// <summary>Where IBC's own instructions and common habits put it.</summary>
	public static IReadOnlyList<string> GetIbcCandidates(
		string home,
		string programFiles,
		string localAppData,
		bool isWindows
	) =>
		isWindows
			?
			[
				@"C:\IBC",
				Path.Combine(programFiles, "IBC"),
				Path.Combine(localAppData, "IBC"),
				Path.Combine(home, "IBC"),
			]
			:
			[
				"/opt/ibc",
				"/opt/IBC",
				"/usr/local/ibc",
				Path.Combine(home, "ibc"),
				Path.Combine(home, "IBC"),
				Path.Combine(home, "opt", "ibc"),
				Path.Combine(home, "Applications", "IBC"),
			];

	/// <summary>Searched (bounded) when no candidate matches, e.g. an unzipped download.</summary>
	public static IReadOnlyList<string> GetIbcSearchRoots(string home, bool isWindows) =>
		isWindows
			? [Path.Combine(home, "Downloads"), Path.Combine(home, "Desktop"), home]
			: [Path.Combine(home, "Downloads"), Path.Combine(home, "Desktop"), "/opt", home];

	/// <summary>IBC's tws-path defaults: Jts (Windows, Linux), Applications (macOS).</summary>
	public static IReadOnlyList<string> GetGatewayCandidates(string home, bool isWindows) =>
		isWindows
			? [@"C:\Jts", Path.Combine(home, "Jts")]
			: [Path.Combine(home, "Jts"), Path.Combine(home, "Applications"), "/Applications"];

	/// <summary>
	/// Accepts the IBC folder, its scripts folder or the start script itself and returns the IBC
	/// folder — or null when there is no start script there.
	/// </summary>
	public static string? ResolveIbcPath(IIbInstallProbe probe, string selected, bool isWindows)
	{
		if (string.IsNullOrWhiteSpace(selected))
			return null;
		var path = Path.TrimEndingDirectorySeparator(selected.Trim());

		if (
			string.Equals(
				Path.GetFileName(path),
				GetIbcScriptName(isWindows),
				StringComparison.OrdinalIgnoreCase
			)
		)
			path = Path.GetDirectoryName(path) ?? path;
		if (string.Equals(Path.GetFileName(path), "scripts", StringComparison.OrdinalIgnoreCase))
			path = Path.GetDirectoryName(path) ?? path;

		return probe.FileExists(IbcHelpers.GetIbcScriptPath(path, isWindows)) ? path : null;
	}

	public static string? FindIbcPath(
		IIbInstallProbe probe,
		IReadOnlyList<string> candidates,
		IReadOnlyList<string> searchRoots,
		bool isWindows
	)
	{
		foreach (var candidate in candidates)
			if (ResolveIbcPath(probe, candidate, isWindows) is { } found)
				return found;

		foreach (var root in searchRoots)
		foreach (
			var script in probe.FindFiles(
				root,
				GetIbcScriptName(isWindows),
				GatewayConstants.IbcSearchDepth
			)
		)
			if (ResolveIbcPath(probe, script, isWindows) is { } found)
				return found;
		return null;
	}

	/// <summary>
	/// Gateway major versions under an IBC tws-path, newest first: ibgateway/1037 (Windows, Linux)
	/// or "IB Gateway 10.37" (macOS).
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
	/// Accepts the Gateway root (IBC's tws-path), its ibgateway folder or one version folder.
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

	/// <summary>IBC release asset name prefix per OS.</summary>
	public static string GetReleaseAssetPrefix(bool isWindows, bool isMacOS) =>
		isWindows ? "IBCWin-"
		: isMacOS ? "IBCMacos-"
		: "IBCLinux-";

	/// <summary>The release asset for this OS, or null.</summary>
	public static IbcReleaseAssetDto? SelectReleaseAsset(
		IReadOnlyList<IbcReleaseAssetDto> assets,
		bool isWindows,
		bool isMacOS
	)
	{
		var prefix = GetReleaseAssetPrefix(isWindows, isMacOS);
		return assets.FirstOrDefault(x =>
			x.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
			&& x.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
		);
	}

	public static string GetIbcScriptName(bool isWindows) =>
		isWindows ? "StartIBC.bat" : "ibcstart.sh";

	[GeneratedRegex(@"^\d{3,4}$")]
	private static partial Regex VersionRegex();

	[GeneratedRegex(@"^IB Gateway (\d+)\.(\d+)$")]
	private static partial Regex MacAppRegex();
}
