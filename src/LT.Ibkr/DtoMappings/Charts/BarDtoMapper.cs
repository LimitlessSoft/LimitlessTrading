using IBKR.Net;
using LSCore.Mapper.Contracts;
using LT.Contracts.Dtos.Charts;

namespace LT.Ibkr.DtoMappings.Charts;

public class BarDtoMapper : ILSCoreMapper<Bar, BarDto>
{
	public BarDto ToMapped(Bar source) =>
		new()
		{
			Time = source.Time,
			Open = source.Open,
			High = source.High,
			Low = source.Low,
			Close = source.Close,
			Volume = source.Volume < 0 ? 0 : source.Volume,
		};
}
