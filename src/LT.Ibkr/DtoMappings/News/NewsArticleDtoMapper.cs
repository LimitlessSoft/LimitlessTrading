using IBKR.Net;
using LSCore.Mapper.Contracts;
using LT.Contracts.Dtos.News;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;

namespace LT.Ibkr.DtoMappings.News;

/// <summary>IB's text type covers both plain text and HTML; the body tells them apart.</summary>
public class NewsArticleDtoMapper : ILSCoreMapper<NewsArticle, NewsArticleDto>
{
	public NewsArticleDto ToMapped(NewsArticle source) =>
		new()
		{
			Kind =
				source.Type == NewsArticleType.Binary
					? NewsArticleKind.Binary
					: NewsHelpers.GetTextKind(source.Text),
			Body = source.Text,
		};
}
