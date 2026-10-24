using LSCore.Mapper.Contracts;
using LT.Contracts.Dtos.Charts;
using LT.Contracts.Entities;

namespace LT.Contracts.DtoMappings.Charts;

public class BarDtoMapper : ILSCoreMapper<BarEntity, BarDto>
{
	public BarDto ToMapped(BarEntity source) =>
		new()
		{
			Time = source.Time,
			Open = source.Open,
			High = source.High,
			Low = source.Low,
			Close = source.Close,
			Volume = source.Volume,
		};
}
