using LSCore.Mapper.Contracts;
using LT.Contracts.DtoMappings.Contracts;
using LT.Contracts.Dtos.Settings;
using LT.Contracts.Entities;

namespace LT.Contracts.DtoMappings.Settings;

public class TickerSettingsDtoMapper : ILSCoreMapper<TickerSettingsEntity, TickerSettingsDto>
{
	public TickerSettingsDto ToMapped(TickerSettingsEntity source) =>
		new()
		{
			Contract = new ContractDtoMapper().ToMapped(source.Contract),
			ChartLiveUpdateMode = source.ChartLiveUpdateMode,
		};
}
