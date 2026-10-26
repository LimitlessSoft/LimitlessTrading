using IBKR.Net;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Enums;
using LT.Ibkr.DtoMappings.Contracts;
using LT.Ibkr.DtoMappings.News;

namespace LT.Tests.Ibkr;

public class NewsMapperTests
{
	[Fact]
	public void Headline_metadata_becomes_language_and_sentiment()
	{
		var headline = new NewsHeadlineDtoMapper().ToMapped(
			new NewsHeadline(
				new DateTimeOffset(2026, 10, 26, 15, 3, 0, TimeSpan.FromHours(2)),
				"DJ-N",
				"DJ-N$1fa3b986",
				" Apple Unveils New iPhone ",
				"A:800015:L:en:K:0.42:C:0.9"
			)
		);

		Assert.Equal(
			("DJ-N", "DJ-N$1fa3b986", "Apple Unveils New iPhone"),
			(headline.ProviderCode, headline.ArticleId, headline.Headline)
		);
		Assert.Equal(new DateTimeOffset(2026, 10, 26, 13, 3, 0, TimeSpan.Zero), headline.Time);
		Assert.Equal(TimeSpan.Zero, headline.Time.Offset);
		Assert.Equal(
			("en", 0.42, 0.9),
			(headline.Language, headline.Sentiment, headline.Confidence)
		);
	}

	[Theory]
	[InlineData(NewsArticleType.Text, "<pre>a&#10;b</pre>", NewsArticleKind.Html)]
	[InlineData(NewsArticleType.Text, "plain words", NewsArticleKind.Text)]
	[InlineData(NewsArticleType.Binary, "JVBERi0x", NewsArticleKind.Binary)]
	public void Article_kind_comes_from_the_type_and_the_body(
		NewsArticleType type,
		string body,
		NewsArticleKind kind
	) => Assert.Equal(kind, new NewsArticleDtoMapper().ToMapped(new NewsArticle(type, body)).Kind);

	[Fact]
	public void A_broad_tape_contract_keeps_its_provider_exchange_and_security_type()
	{
		var contract = new IbkrContractMapper().ToMapped(
			new ContractDto
			{
				ConId = 100283238,
				Symbol = "BRFG:BRFG_ALL",
				SecurityType = "NEWS",
				Exchange = "BRFG",
			}
		);

		Assert.Equal(
			(100283238, "BRFG:BRFG_ALL", "NEWS", "BRFG"),
			(contract.ConId, contract.Symbol, contract.SecurityType, contract.Exchange)
		);
	}
}
