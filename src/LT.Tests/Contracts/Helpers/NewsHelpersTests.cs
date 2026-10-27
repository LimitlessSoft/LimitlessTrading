using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.News;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;

namespace LT.Tests.Contracts.Helpers;

public class NewsHelpersTests
{
	private static NewsHeadlineDto Headline(
		string articleId,
		int minute,
		string provider = "DJ-N"
	) =>
		new()
		{
			SourceId = "ibkr",
			ProviderCode = provider,
			ArticleId = articleId,
			Time = new DateTimeOffset(2026, 10, 26, 12, minute, 0, TimeSpan.Zero),
			Headline = $"Headline {articleId}",
		};

	[Fact]
	public void Extra_data_gives_the_language()
	{
		var extra = NewsHelpers.ParseExtraData("A:800015:L:en");

		Assert.Equal("en", extra.Language);
		Assert.Null(extra.Sentiment);
		Assert.Null(extra.Confidence);
	}

	[Fact]
	public void Extra_data_gives_sentiment_and_confidence_when_scored()
	{
		var extra = NewsHelpers.ParseExtraData("{A:800015:L:en:K:-0.97:C:0.97}");

		Assert.Equal("en", extra.Language);
		Assert.Equal(-0.97, extra.Sentiment);
		Assert.Equal(0.97, extra.Confidence);
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("A:800015:L:en:E")]
	[InlineData("K:n/a")]
	public void Extra_data_ignores_flags_missing_and_unreadable_values(string? extraData)
	{
		var extra = NewsHelpers.ParseExtraData(extraData);

		Assert.Null(extra.Sentiment);
	}

	[Fact]
	public void Pre_text_keeps_its_lines_and_decodes_entities()
	{
		var text = NewsHelpers.HtmlToPlainText(
			"<pre>Apple Inc. &amp; partners&#10;&#10;&#10;&#10;Second line  indented</pre>"
		);

		Assert.Equal("Apple Inc. & partners\n\nSecond line  indented", text);
	}

	[Fact]
	public void Html_becomes_paragraphs_without_tags()
	{
		var text = NewsHelpers.HtmlToPlainText(
			"<html><head><style>p{}</style></head><body><p>First\n   paragraph with <b>bold</b>.</p>"
				+ "<p>Second&nbsp;one<br/>next line</p><ul><li>One</li><li>Two</li></ul></body></html>"
		);

		Assert.Equal("First paragraph with bold.\n\nSecond one\nnext line\n\n• One\n\n• Two", text);
	}

	[Fact]
	public void Text_articles_are_decoded_and_html_is_detected()
	{
		Assert.Equal(NewsArticleKind.Html, NewsHelpers.GetTextKind("<html><body><p>x</p>"));
		Assert.Equal(NewsArticleKind.Html, NewsHelpers.GetTextKind("<pre>x</pre>"));
		Assert.Equal(NewsArticleKind.Text, NewsHelpers.GetTextKind("AAPL < 200 and > 100"));
		Assert.Equal(
			"a\nb",
			NewsHelpers.ToPlainText(
				new NewsArticleDto { Kind = NewsArticleKind.Text, Body = "a&#10;b" }
			)
		);
		Assert.Equal(
			string.Empty,
			NewsHelpers.ToPlainText(
				new NewsArticleDto { Kind = NewsArticleKind.Binary, Body = "JVBERg==" }
			)
		);
	}

	[Fact]
	public void Merge_dedupes_on_source_provider_and_article_and_sorts_newest_first()
	{
		var history = new[] { Headline("b", 2), Headline("a", 1) };
		var live = new[] { Headline("c", 3), Headline("b", 2), Headline("a", 1, "BRFG") };

		var merged = NewsHelpers.Merge(history, live, 10);

		Assert.Equal(["c", "b", "a", "a"], merged.Select(x => x.ArticleId));
		Assert.Equal(
			["DJ-N", "BRFG"],
			merged.Where(x => x.ArticleId == "a").Select(x => x.ProviderCode)
		);
	}

	[Fact]
	public void Merge_keeps_at_most_the_newest()
	{
		var merged = NewsHelpers.Merge([Headline("a", 1), Headline("b", 2)], [Headline("c", 3)], 2);

		Assert.Equal(["c", "b"], merged.Select(x => x.ArticleId));
	}

	[Fact]
	public void Insert_places_by_time_and_skips_a_headline_already_listed()
	{
		var list = new List<NewsHeadlineDto>();
		var keys = new HashSet<string>();

		Assert.Equal(0, NewsHelpers.Insert(list, keys, Headline("a", 1)));
		Assert.Equal(0, NewsHelpers.Insert(list, keys, Headline("c", 3)));
		Assert.Equal(1, NewsHelpers.Insert(list, keys, Headline("b", 2)));
		Assert.Equal(-1, NewsHelpers.Insert(list, keys, Headline("b", 2)));
		// Same time: after the one already there
		Assert.Equal(2, NewsHelpers.Insert(list, keys, Headline("b2", 2)));

		Assert.Equal(["c", "b", "b2", "a"], list.Select(x => x.ArticleId));
	}

	[Fact]
	public void Broad_tape_prefers_the_all_tape_else_the_first()
	{
		ContractDto smu = new() { Symbol = "BRFG:BRFG_SMU" };
		ContractDto all = new() { Symbol = "BRFG:BRFG_ALL" };

		Assert.Same(all, NewsHelpers.PickBroadTape([smu, all]));
		Assert.Same(smu, NewsHelpers.PickBroadTape([smu]));
		Assert.Null(NewsHelpers.PickBroadTape([]));
	}

	[Fact]
	public async Task Broad_tape_lookup_is_remembered_also_when_there_is_none()
	{
		var cache = new Dictionary<string, ContractDto?>();
		var calls = 0;
		Task<IReadOnlyList<ContractDto>> Fetch(string code, CancellationToken _)
		{
			calls++;
			return Task.FromResult<IReadOnlyList<ContractDto>>(
				code == "DJNL" ? [new ContractDto { Symbol = "DJNL:DJNL_ALL" }] : []
			);
		}

		var ct = TestContext.Current.CancellationToken;
		Assert.NotNull(await NewsHelpers.ResolveBroadTapeAsync(cache, "DJNL", Fetch, ct));
		Assert.Null(await NewsHelpers.ResolveBroadTapeAsync(cache, "BZ", Fetch, ct));
		Assert.NotNull(await NewsHelpers.ResolveBroadTapeAsync(cache, "DJNL", Fetch, ct));
		Assert.Null(await NewsHelpers.ResolveBroadTapeAsync(cache, "BZ", Fetch, ct));

		Assert.Equal(2, calls);
	}

	[Fact]
	public async Task A_dow_jones_provider_resolves_to_the_shared_tape_without_a_lookup()
	{
		var cache = new Dictionary<string, ContractDto?>();
		Task<IReadOnlyList<ContractDto>> Fetch(string code, CancellationToken _) =>
			throw new InvalidOperationException("Looked up " + code);

		var tape = await NewsHelpers.ResolveBroadTapeAsync(
			cache,
			"DJ-RTE",
			Fetch,
			TestContext.Current.CancellationToken
		);

		Assert.NotNull(tape);
		Assert.Equal(("DJ:N_DJN", "NEWS", "DJ"), (tape.Symbol, tape.SecurityType, tape.Exchange));
		Assert.Null(NewsHelpers.GetSharedBroadTape("BRFG"));
	}

	[Fact]
	public void Broad_tapes_are_grouped_and_only_shared_ones_are_filtered()
	{
		var brfg = new ContractDto
		{
			ConId = 2,
			Symbol = "BRFG:BRFG_ALL",
			Exchange = "BRFG",
		};
		var groups = NewsHelpers.GroupBroadTapes([
			("DJ-N", NewsHelpers.GetSharedBroadTape("DJ-N")!),
			("BRFG", brfg),
			("DJ-RTA", NewsHelpers.GetSharedBroadTape("DJ-RTA")!),
		]);

		Assert.Equal(2, groups.Count);
		Assert.Equal("DJ:N_DJN", groups[0].Tape.Symbol);
		Assert.Equal(["DJ-N", "DJ-RTA"], groups[0].ProviderCodes!.Order());
		Assert.Same(brfg, groups[1].Tape);
		Assert.Null(groups[1].ProviderCodes);
	}

	[Fact]
	public void Provider_codes_are_those_of_the_source()
	{
		NewsProviderDto[] providers =
		[
			new() { SourceId = "ibkr", Code = "BRFG" },
			new() { SourceId = "rss", Code = "Reuters" },
			new() { SourceId = "ibkr", Code = "DJ-N" },
		];

		Assert.Equal(["BRFG", "DJ-N"], NewsHelpers.GetProviderCodes(providers, "ibkr"));
		Assert.Equal("rss:Reuters", NewsHelpers.GetProviderKey(providers[1]));
	}

	[Fact]
	public void Filter_matches_every_word_in_headline_provider_or_symbol()
	{
		var headline = Headline("a", 1);
		headline.Headline = "Apple unveils new iPhone";
		headline.Symbol = "AAPL";

		Assert.True(NewsHelpers.Matches(headline, null));
		Assert.True(NewsHelpers.Matches(headline, "iphone apple"));
		Assert.True(NewsHelpers.Matches(headline, "aapl dj-n"));
		Assert.False(NewsHelpers.Matches(headline, "apple android"));
	}

	[Fact]
	public void Documents_get_a_safe_name_and_an_extension_from_their_content()
	{
		var headline = Headline("BRFG$12/34", 1, "BRFG");
		var pdf = NewsHelpers.DecodeBinary(
			new NewsArticleDto { Kind = NewsArticleKind.Binary, Body = "JVBE\nRi0x" }
		);

		Assert.Equal("news-BRFG-BRFG_12_34.pdf", NewsHelpers.GetDocumentFileName(headline, pdf));
		Assert.Equal("news-BRFG-BRFG_12_34.bin", NewsHelpers.GetDocumentFileName(headline, [1, 2]));
	}
}
