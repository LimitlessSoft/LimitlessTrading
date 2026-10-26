using System.Text.RegularExpressions;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.PatchNotes;
using LT.Contracts.Dtos.Updates;
using LT.Contracts.Entities;
using LT.Contracts.Enums;

namespace LT.Contracts.Helpers;

public static partial class PatchNotesHelpers
{
	/// <summary>
	/// The changes listed in a release body. GitHub's generated notes are a "What's Changed" list
	/// of "* Title by @author in https://…/pull/48" lines; the author and link are dropped from the
	/// text, the pull request kept. Headings, "New Contributors" and the "Full Changelog" line are
	/// skipped, as are rules and HTML comments; any other text line (hand-written notes) is an entry as it is.
	/// </summary>
	public static List<PatchNoteEntryDto> ParseEntries(string? body)
	{
		var entries = new List<PatchNoteEntryDto>();
		if (string.IsNullOrWhiteSpace(body))
			return entries;

		var skipSection = false;
		foreach (var raw in body.Split('\n'))
		{
			var line = raw.Trim();
			if (line.Length == 0)
				continue;
			if (line.StartsWith('#'))
			{
				skipSection = line.Contains("New Contributors", StringComparison.OrdinalIgnoreCase);
				continue;
			}
			// Rules (---) and HTML comments aren't changes either
			if (
				skipSection
				|| line.StartsWith("**Full Changelog**", StringComparison.Ordinal)
				|| line.StartsWith("<!--", StringComparison.Ordinal)
				|| line.All(x => x is '-' or '*' or '_' or ' ')
			)
				continue;

			var text = BulletRegex().Replace(line, string.Empty, 1).Trim();
			if (text.Length == 0)
				continue;
			var entry = new PatchNoteEntryDto { Text = text };
			if (AuthorSuffixRegex().Match(text) is { Success: true } suffix)
			{
				entry.Text = text[..suffix.Index].TrimEnd();
				SetPullRequest(entry, suffix.Groups["link"].Value);
			}
			if (entry.Text.Length > 0)
				entries.Add(entry);
		}
		return entries;
	}

	/// <summary>What is cached of a release fetched from GitHub.</summary>
	public static PatchNotesReleaseEntity ToEntity(AppReleaseDto release) =>
		new()
		{
			Tag = release.Tag,
			PageUrl = release.PageUrl,
			PublishedAt = release.PublishedAt,
			Body = release.Body,
		};

	public static PatchNotesReleaseDto ToRelease(
		PatchNotesReleaseEntity release,
		string currentVersion
	)
	{
		var version = UpdateHelpers.NormalizeVersion(release.Tag);
		return new PatchNotesReleaseDto
		{
			Version = version,
			PageUrl = release.PageUrl,
			PublishedAt = release.PublishedAt,
			IsCurrent = UpdateHelpers.Compare(version, currentVersion) == 0,
			Entries = ParseEntries(release.Body),
		};
	}

	/// <summary>A status of <paramref name="state"/> showing the cached releases, newest first.</summary>
	public static PatchNotesStatusDto CreateStatus(
		PatchNotesState state,
		string currentVersion,
		PatchNotesStateEntity cache,
		string? error = null
	) =>
		new()
		{
			State = state,
			CurrentVersion = currentVersion,
			FetchedAt = cache.FetchedAt,
			Error = error,
			Releases = cache
				.Releases.Select(x => ToRelease(x, currentVersion))
				.OrderByDescending(x => x.PublishedAt ?? DateTimeOffset.MinValue)
				.ToList(),
		};

	/// <summary>
	/// Whether this start is the first on a newer version than <paramref name="lastSeenVersion"/>:
	/// never on the very first start (nothing seen) or from a source build.
	/// </summary>
	public static bool IsNewVersion(string? lastSeenVersion, string currentVersion) =>
		lastSeenVersion != null
		&& !UpdateHelpers.IsDevVersion(currentVersion)
		&& UpdateHelpers.IsNewer(currentVersion, lastSeenVersion);

	// "https://github.com/…/pull/48" or "#48"
	private static void SetPullRequest(PatchNoteEntryDto entry, string link)
	{
		if (PullUrlRegex().Match(link) is { Success: true } url)
		{
			entry.PullRequestNumber = int.Parse(url.Groups["number"].Value);
			entry.PullRequestUrl = link;
		}
		else if (PullNumberRegex().Match(link) is { Success: true } number)
		{
			entry.PullRequestNumber = int.Parse(number.Groups["number"].Value);
			entry.PullRequestUrl = $"{AppConstants.RepositoryUrl}/pull/{entry.PullRequestNumber}";
		}
	}

	[GeneratedRegex(@"^[*+-]\s+")]
	private static partial Regex BulletRegex();

	// " by @author in <link>" at the end; bot authors look like @dependabot[bot]
	[GeneratedRegex(@"\s+by\s+@[\w.\-\[\]]+\s+in\s+(?<link>\S+)$")]
	private static partial Regex AuthorSuffixRegex();

	[GeneratedRegex(@"^https?://\S+/pull/(?<number>\d+)/?$")]
	private static partial Regex PullUrlRegex();

	[GeneratedRegex(@"^#(?<number>\d+)$")]
	private static partial Regex PullNumberRegex();
}
