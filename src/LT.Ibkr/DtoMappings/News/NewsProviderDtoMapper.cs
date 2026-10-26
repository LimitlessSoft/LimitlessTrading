using IBKR.Net;
using LSCore.Mapper.Contracts;
using LT.Contracts.Dtos.News;

namespace LT.Ibkr.DtoMappings.News;

/// <summary>SourceId is left for the news source to set.</summary>
public class NewsProviderDtoMapper : ILSCoreMapper<NewsProvider, NewsProviderDto>
{
	public NewsProviderDto ToMapped(NewsProvider source) =>
		new() { Code = source.Code, Name = source.Name };
}
