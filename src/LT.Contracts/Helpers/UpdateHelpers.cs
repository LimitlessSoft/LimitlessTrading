using LT.Contracts.Constants;
using LT.Contracts.Dtos.Updates;
using LT.Contracts.Enums;

namespace LT.Contracts.Helpers;

public static class UpdateHelpers
{
	/// <summary>"v0.2.0+abc" → "0.2.0".</summary>
	public static string NormalizeVersion(string version)
	{
		var value = version.Trim().TrimStart('v', 'V');
		var metadata = value.IndexOf('+');
		return metadata >= 0 ? value[..metadata] : value;
	}

	/// <summary>
	/// Semantic version order: numbers first, then a release beats its own pre-release
	/// (0.2.0 &gt; 0.2.0-beta.1), pre-releases compare by text. Unparsable versions are never newer.
	/// </summary>
	public static bool IsNewer(string candidate, string current) =>
		Compare(candidate, current) is > 0;

	public static int? Compare(string left, string right)
	{
		if (
			Parse(left) is not var (leftCore, leftPre)
			|| Parse(right) is not var (rightCore, rightPre)
		)
			return null;
		var core = leftCore.CompareTo(rightCore);
		if (core != 0)
			return core;
		return (leftPre, rightPre) switch
		{
			(null, null) => 0,
			(null, _) => 1,
			(_, null) => -1,
			_ => string.CompareOrdinal(leftPre, rightPre),
		};
	}

	/// <summary>The release archive for this system, by its file name ending.</summary>
	public static AppReleaseAssetDto? SelectAsset(
		IEnumerable<AppReleaseAssetDto> assets,
		string suffix
	) => assets.FirstOrDefault(x => x.Name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));

	/// <summary>Builds from source carry the dev suffix ("0.0.0-dev") and never update themselves.</summary>
	public static bool IsDevVersion(string version) =>
		version.Contains(AppConstants.DevVersionSuffix, StringComparison.OrdinalIgnoreCase);

	/// <summary>
	/// Whether the file's SHA-256 matches GitHub's "sha256:&lt;hex&gt;" digest; true when there is
	/// no digest to compare (older releases) or it uses another algorithm.
	/// </summary>
	public static bool MatchesDigest(string? digest, byte[] sha256)
	{
		const string prefix = "sha256:";
		if (digest is null || !digest.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
			return true;
		return string.Equals(
			digest[prefix.Length..],
			Convert.ToHexString(sha256),
			StringComparison.OrdinalIgnoreCase
		);
	}

	/// <summary>A status of <paramref name="state"/>, describing <paramref name="latest"/> if known.</summary>
	public static UpdateStatusDto CreateStatus(
		UpdateState state,
		string currentVersion,
		AppReleaseDto? latest,
		string? cannotInstallReason,
		double? progress = null,
		string? error = null
	) =>
		new()
		{
			State = state,
			CurrentVersion = currentVersion,
			LatestVersion = latest is null ? null : NormalizeVersion(latest.Tag),
			ReleasePageUrl = latest?.PageUrl,
			CannotInstallReason = cannotInstallReason,
			Progress = progress,
			Error = error,
		};

	private static (Version Core, string? PreRelease)? Parse(string version)
	{
		var value = NormalizeVersion(version);
		var dash = value.IndexOf('-');
		var core = dash >= 0 ? value[..dash] : value;
		var preRelease = dash >= 0 ? value[(dash + 1)..] : null;
		if (!Version.TryParse(core.Count(x => x == '.') == 0 ? core + ".0" : core, out var parsed))
			return null;
		// 0.2 and 0.2.0 compare equal
		return (new Version(parsed.Major, parsed.Minor, Math.Max(0, parsed.Build)), preRelease);
	}
}
