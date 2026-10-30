using LSCore.Mapper.Contracts;
using LT.Contracts.DtoMappings.Contracts;
using LT.Contracts.Dtos.Settings;
using LT.Contracts.Entities;

namespace LT.Contracts.DtoMappings.Settings;

public class TickerSettingsEntityMapper : ILSCoreMapper<TickerSettingsDto, TickerSettingsEntity>
{
	public TickerSettingsEntity ToMapped(TickerSettingsDto source) =>
		new()
		{
			Contract = new ContractEntityMapper().ToMapped(source.Contract),
			ChartLiveUpdateMode = source.ChartLiveUpdateMode,
		};
}
