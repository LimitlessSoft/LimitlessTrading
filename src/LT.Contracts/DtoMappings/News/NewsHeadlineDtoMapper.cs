using LSCore.Mapper.Contracts;
using LT.Contracts.Dtos.News;
using LT.Contracts.Entities;

namespace LT.Contracts.DtoMappings.News;

public class NewsHeadlineDtoMapper : ILSCoreMapper<NewsHeadlineEntity, NewsHeadlineDto>
{
	public NewsHeadlineDto ToMapped(NewsHeadlineEntity source) =>
		new()
		{
			SourceId = source.SourceId,
			ProviderCode = source.ProviderCode,
			ArticleId = source.ArticleId,
			Time = source.Time,
			Headline = source.Headline,
			Summary = source.Summary,
			Url = source.Url,
			Symbol = source.Symbols?.FirstOrDefault() ?? string.Empty,
			Symbols = source.Symbols is { } symbols ? [.. symbols] : [],
			ContractId = source.ContractId,
			Language = source.Language,
			Sentiment = source.Sentiment,
			Confidence = source.Confidence,
		};
}
