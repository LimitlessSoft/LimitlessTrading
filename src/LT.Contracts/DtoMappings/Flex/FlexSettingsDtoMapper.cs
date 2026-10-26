using LSCore.Mapper.Contracts;
using LT.Contracts.Dtos.Flex;
using LT.Contracts.Entities;

namespace LT.Contracts.DtoMappings.Flex;

public class FlexSettingsDtoMapper : ILSCoreMapper<FlexSettingsEntity, FlexSettingsDto>
{
	public FlexSettingsDto ToMapped(FlexSettingsEntity source) =>
		new()
		{
			Token = source.Token,
			ActivityQueryId = source.ActivityQueryId,
			TradeConfirmQueryId = source.TradeConfirmQueryId,
		};
}
