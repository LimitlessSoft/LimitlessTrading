using LSCore.Mapper.Contracts;
using LT.Contracts.Dtos.Performance;
using LT.Contracts.Entities;

namespace LT.Contracts.DtoMappings.Performance;

public class PnlSampleDtoMapper : ILSCoreMapper<PnlSampleEntity, PnlSampleDto>
{
	public PnlSampleDto ToMapped(PnlSampleEntity source) =>
		new() { Time = source.Time, Value = source.DailyPnl };
}
