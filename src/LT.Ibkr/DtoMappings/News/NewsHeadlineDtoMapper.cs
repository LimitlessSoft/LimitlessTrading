using IBKR.Net;
using LSCore.Mapper.Contracts;
using LT.Contracts.Dtos.News;
using LT.Contracts.Helpers;

namespace LT.Ibkr.DtoMappings.News;

/// <summary>Source, contract and symbol are left for the news source to set.</summary>
public class NewsHeadlineDtoMapper : ILSCoreMapper<NewsHeadline, NewsHeadlineDto>
{
	public NewsHeadlineDto ToMapped(NewsHeadline source)
	{
		var extra = NewsHelpers.ParseExtraData(source.ExtraData);
		return new NewsHeadlineDto
		{
			ProviderCode = source.ProviderCode,
			ArticleId = source.ArticleId,
			Time = source.Time.ToUniversalTime(),
			Headline = source.Headline.Trim(),
			Language = extra.Language,
			Sentiment = extra.Sentiment,
			Confidence = extra.Confidence,
		};
	}
}
