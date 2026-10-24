using IBApi;
using LSCore.Mapper.Contracts;
using LT.Contracts.Dtos.Charts;
using LT.Contracts.Helpers;

namespace LT.Ibkr.DtoMappings.Charts;

public class IbBarDtoMapper : ILSCoreMapper<Bar, BarDto>
{
	public BarDto ToMapped(Bar source) =>
		new()
		{
			Time = IbTimeHelpers.Parse(source.Time) ?? DateTimeOffset.MinValue,
			Open = source.Open,
			High = source.High,
			Low = source.Low,
			Close = source.Close,
			Volume = source.Volume < 0 ? 0 : source.Volume,
		};
}
