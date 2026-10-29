using LT.Contracts.Constants;
using LT.Contracts.Dtos.News;
using LT.Contracts.Helpers;

namespace LT.Tests.Contracts.Helpers;

/// <summary>Same story across sources, alert matches, archive search and saved provider selections.</summary>
public class NewsHelpersArchiveTests
{
	private static NewsHeadlineDto Headline(
		string source,
		string provider,
		string id,
		int minute,
		string text,
		params string[] symbols
	) =>
		new()
		{
			SourceId = source,
			ProviderCode = provider,
			ArticleId = id,
			Time = new DateTimeOffset(2026, 10, 29, 12, 0, 0, TimeSpan.Zero).AddMinutes(minute),
			Headline = text,
			Symbol = symbols.FirstOrDefault() ?? string.Empty,
			Symbols = symbols,
		};

	[Fact]
	public void The_story_key_ignores_case_and_punctuation() =>
		Assert.Equal(
			NewsHelpers.GetStoryKey(
				Headline("lt", "PRN", "1", 0, "Aptar Reports Third-Quarter 2026 Results")
			),
			NewsHelpers.GetStoryKey(
				Headline("ibkr", "DJ-N", "2", 0, "APTAR REPORTS THIRD QUARTER 2026 RESULTS.")
			)
		);

	[Fact]
	public void The_same_story_from_another_provider_within_the_window_is_a_repeat()
	{
		var stories = new Dictionary<string, List<(DateTimeOffset, string)>>();

		Assert.False(
			NewsHelpers.IsRepeatedStory(
				stories,
				Headline("lt", "PRN", "1", 0, "Aptar Reports Q3"),
				NewsConstants.StoryWindow
			)
		);
		Assert.True(
			NewsHelpers.IsRepeatedStory(
				stories,
				Headline("ibkr", "DJ-N", "2", 4, "APTAR reports Q3"),
				NewsConstants.StoryWindow
			)
		);
		Assert.False(
			NewsHelpers.IsRepeatedStory(
				stories,
				Headline("ibkr", "BRFG", "3", 30, "Aptar reports Q3"),
				NewsConstants.StoryWindow
			)
		);
	}

	[Fact]
	public void One_provider_repeating_a_headline_is_never_a_repeat()
	{
		var stories = new Dictionary<string, List<(DateTimeOffset, string)>>();

		Assert.False(
			NewsHelpers.IsRepeatedStory(
				stories,
				Headline("lt", "SEC", "1", 0, "AAPL 4: Statement of changes"),
				NewsConstants.StoryWindow
			)
		);
		Assert.False(
			NewsHelpers.IsRepeatedStory(
				stories,
				Headline("lt", "SEC", "2", 0, "AAPL 4: Statement of changes"),
				NewsConstants.StoryWindow
			)
		);
	}

	[Fact]
	public void Merging_keeps_the_first_published_copy_of_a_story()
	{
		var merged = NewsHelpers.MergeStories(
			[
				Headline("ibkr", "DJ-N", "dj", 3, "Aptar Reports Q3 Results"),
				Headline("lt", "PRN", "prn", 0, "Aptar Reports Q3 Results"),
				Headline("lt", "PRN", "prn", 0, "Aptar Reports Q3 Results"),
				Headline("lt", "FED", "fed", 1, "FOMC statement"),
			],
			10,
			NewsConstants.StoryWindow
		);

		Assert.Equal(["fed", "prn"], merged.Select(x => x.ArticleId));
	}

	[Fact]
	public void Keywords_are_split_on_commas_and_lines()
	{
		Assert.Equal(
			["FDA approval", "acquire", "guidance"],
			NewsHelpers.ParseKeywords(" FDA approval, acquire;\nguidance, ,ACQUIRE")
		);
		Assert.Empty(NewsHelpers.ParseKeywords(null));
	}

	[Theory]
	[InlineData("Company wins FDA Approval for drug", true)]
	[InlineData("Company to acquire rival", true)]
	[InlineData("Company acquires rival", false)]
	[InlineData("Quiet day", false)]
	public void A_keyword_matches_as_a_whole_word_in_any_case(string text, bool matches) =>
		Assert.Equal(
			matches,
			NewsHelpers.MatchesAlert(
				Headline("lt", "PRN", "1", 0, text),
				["fda approval", "acquire"],
				new HashSet<string>()
			)
		);

	[Fact]
	public void A_headline_about_a_watchlist_symbol_matches()
	{
		var symbols = new HashSet<string> { "BRK-B", "AAPL" };

		Assert.True(
			NewsHelpers.MatchesAlert(Headline("lt", "PRN", "1", 0, "x", "BRK.B"), [], symbols)
		);
		Assert.False(
			NewsHelpers.MatchesAlert(Headline("lt", "PRN", "1", 0, "x", "MSFT"), [], symbols)
		);
	}

	[Fact]
	public void A_keyword_in_the_summary_matches_too()
	{
		var headline = Headline("lt", "PRN", "1", 0, "Results");
		headline.Summary = "<p>The board approved a buyback.</p>";

		Assert.True(NewsHelpers.MatchesAlert(headline, ["buyback"], new HashSet<string>()));
	}

	[Fact]
	public void Archive_search_looks_at_headline_summary_and_symbols()
	{
		var headline = Headline("lt", "PRN", "1", 0, "Kimberly-Clark closes notes offering", "KMB");
		headline.Summary = "A combined aggregate principal amount";

		Assert.True(NewsHelpers.MatchesSearch(headline, "kimberly principal"));
		Assert.True(NewsHelpers.MatchesSearch(headline, "kmb"));
		Assert.False(NewsHelpers.MatchesSearch(headline, "kimberly buyback"));
	}

	private static NewsProviderDto Provider(string source, string code, bool byDefault = true) =>
		new()
		{
			SourceId = source,
			Code = code,
			SelectedByDefault = byDefault,
		};

	[Fact]
	public void Without_a_saved_choice_providers_take_their_default()
	{
		Assert.True(NewsHelpers.IsProviderSelected(null, Provider("lt", "SEC")));
		Assert.False(NewsHelpers.IsProviderSelected(null, Provider("lt", "CNBC", false)));
		// Saved before the app had its own source: IB's choice holds, ours take their default
		var old = new HashSet<string> { "ibkr:BRFG" };
		Assert.True(NewsHelpers.IsProviderSelected(old, Provider("lt", "SEC")));
		Assert.False(NewsHelpers.IsProviderSelected(old, Provider("ibkr", "DJ-N")));
	}

	[Fact]
	public void A_saved_selection_keeps_sources_not_shown_and_none_of_a_source()
	{
		var saved = NewsHelpers.MergeProviderSelection(
			[(Provider("lt", "SEC"), false), (Provider("lt", "CNBC", false), false)],
			["ibkr:BRFG", "ibkr:", "lt:", "lt:SEC"]
		);

		Assert.Equal(["ibkr:BRFG", "ibkr:", "lt:"], saved);
		var set = saved.ToHashSet();
		Assert.False(NewsHelpers.IsProviderSelected(set, Provider("lt", "SEC")));
		Assert.True(NewsHelpers.IsProviderSelected(set, Provider("ibkr", "BRFG")));
		Assert.False(NewsHelpers.IsProviderSelected(set, Provider("ibkr", "DJ-N")));
	}
}
