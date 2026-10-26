using LT.Contracts.Entities;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;

namespace LT.Tests.Contracts.Helpers;

public class PatchNotesHelpersTests
{
	// v0.10.0's notes as GitHub generated them
	private const string GeneratedBody =
		"## What's Changed\n"
		+ "* Pin watchlist tickers to the top by @AleksaRistic216 in https://github.com/LimitlessSoft/LimitlessTrading/pull/48\n"
		+ "* Chart templates: follow and save on change by @AleksaRistic216 in https://github.com/LimitlessSoft/LimitlessTrading/pull/47\n"
		+ "\n\n"
		+ "**Full Changelog**: https://github.com/LimitlessSoft/LimitlessTrading/compare/v0.9.0...v0.10.0";

	[Fact]
	public void ParseEntries_GeneratedNotes_DropsAuthorAndKeepsThePullRequest()
	{
		var entries = PatchNotesHelpers.ParseEntries(GeneratedBody);

		Assert.Equal(2, entries.Count);
		Assert.Equal("Pin watchlist tickers to the top", entries[0].Text);
		Assert.Equal(48, entries[0].PullRequestNumber);
		Assert.Equal(
			"https://github.com/LimitlessSoft/LimitlessTrading/pull/48",
			entries[0].PullRequestUrl
		);
		Assert.Equal("Chart templates: follow and save on change", entries[1].Text);
		Assert.Equal(47, entries[1].PullRequestNumber);
	}

	[Fact]
	public void ParseEntries_WindowsLineEndings_AreTrimmed()
	{
		var entries = PatchNotesHelpers.ParseEntries(GeneratedBody.Replace("\n", "\r\n"));

		Assert.Equal(2, entries.Count);
		Assert.Equal(48, entries[0].PullRequestNumber);
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   \n\n")]
	[InlineData(
		"**Full Changelog**: https://github.com/LimitlessSoft/LimitlessTrading/compare/v0.2.0...v0.3.0"
	)]
	public void ParseEntries_NothingListed_IsEmpty(string? body) =>
		Assert.Empty(PatchNotesHelpers.ParseEntries(body));

	[Fact]
	public void ParseEntries_NewContributors_AreNotChanges()
	{
		var body =
			"## What's Changed\n"
			+ "* Fix the chart by @someone in https://github.com/LimitlessSoft/LimitlessTrading/pull/60\n"
			+ "\n## New Contributors\n"
			+ "* @someone made their first contribution in https://github.com/LimitlessSoft/LimitlessTrading/pull/60\n";

		var entry = Assert.Single(PatchNotesHelpers.ParseEntries(body));
		Assert.Equal("Fix the chart", entry.Text);
	}

	[Fact]
	public void ParseEntries_BotAuthorAndShortLink_AreRecognised()
	{
		var body =
			"- Bump Avalonia by @dependabot[bot] in https://github.com/LimitlessSoft/LimitlessTrading/pull/70/\n"
			+ "+ Fix a typo by @user-name in #71";

		var entries = PatchNotesHelpers.ParseEntries(body);

		Assert.Equal("Bump Avalonia", entries[0].Text);
		Assert.Equal(70, entries[0].PullRequestNumber);
		Assert.Equal("Fix a typo", entries[1].Text);
		Assert.Equal(71, entries[1].PullRequestNumber);
		Assert.Equal(
			"https://github.com/LimitlessSoft/LimitlessTrading/pull/71",
			entries[1].PullRequestUrl
		);
	}

	[Fact]
	public void ParseEntries_HandWrittenNotes_AreKeptAsTheyAre()
	{
		var body =
			"# Highlights\n"
			+ "A faster chart.\n"
			+ "---\n"
			+ "<!-- internal -->\n"
			+ "* Fixed the login dialog\n"
			+ "* Credits go to @someone";

		var entries = PatchNotesHelpers.ParseEntries(body);

		Assert.Equal(
			["A faster chart.", "Fixed the login dialog", "Credits go to @someone"],
			entries.Select(x => x.Text)
		);
		Assert.All(entries, x => Assert.Null(x.PullRequestNumber));
	}

	[Fact]
	public void CreateStatus_ListsNewestFirstAndMarksTheRunningVersion()
	{
		var cache = new PatchNotesStateEntity
		{
			Releases =
			[
				Release("v0.9.0", new DateTimeOffset(2026, 10, 26, 19, 10, 0, TimeSpan.Zero)),
				Release("v0.10.0", new DateTimeOffset(2026, 10, 26, 19, 29, 0, TimeSpan.Zero)),
				Release("v0.1.0", null),
			],
		};

		var status = PatchNotesHelpers.CreateStatus(PatchNotesState.Loaded, "0.9.0", cache);

		Assert.Equal(["0.10.0", "0.9.0", "0.1.0"], status.Releases.Select(x => x.Version));
		Assert.Equal([false, true, false], status.Releases.Select(x => x.IsCurrent));
		Assert.Equal(48, status.Releases[0].Entries[0].PullRequestNumber);
	}

	[Theory]
	[InlineData(null, "0.10.0", false)]
	[InlineData("0.9.0", "0.10.0", true)]
	[InlineData("0.10.0", "0.10.0", false)]
	[InlineData("0.10.0", "0.9.0", false)]
	[InlineData("0.9.0", "0.0.0-dev", false)]
	public void IsNewVersion_OnlyAfterAnUpgrade(string? lastSeen, string current, bool expected) =>
		Assert.Equal(expected, PatchNotesHelpers.IsNewVersion(lastSeen, current));

	private static PatchNotesReleaseEntity Release(string tag, DateTimeOffset? publishedAt) =>
		new()
		{
			Tag = tag,
			PageUrl = $"https://github.com/LimitlessSoft/LimitlessTrading/releases/tag/{tag}",
			PublishedAt = publishedAt,
			Body = GeneratedBody,
		};
}
