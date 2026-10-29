using LSCore.Mapper.Contracts;
using LT.Contracts.Dtos.News;
using LT.Contracts.Entities;

namespace LT.Contracts.DtoMappings.News;

/// <summary>A headline as an archive line; the symbol list keeps the primary symbol first.</summary>
public class NewsHeadlineEntityMapper : ILSCoreMapper<NewsHeadlineDto, NewsHeadlineEntity>
{
	public NewsHeadlineEntity ToMapped(NewsHeadlineDto source)
	{
		List<string> symbols = [];
		if (source.Symbol.Length > 0)
			symbols.Add(source.Symbol);
		symbols.AddRange(source.Symbols.Where(x => x.Length > 0 && !symbols.Contains(x)));
		return new NewsHeadlineEntity
		{
			SourceId = source.SourceId,
			ProviderCode = source.ProviderCode,
			ArticleId = source.ArticleId,
			Time = source.Time,
			Headline = source.Headline,
			Summary = source.Summary,
			Url = source.Url,
			Symbols = symbols.Count == 0 ? null : symbols,
			ContractId = source.ContractId,
			Language = source.Language,
			Sentiment = source.Sentiment,
			Confidence = source.Confidence,
		};
	}
}
