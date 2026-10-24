using LSCore.Mapper.Contracts;
using LT.Contracts.Dtos.Settings;
using LT.Contracts.Entities;

namespace LT.Contracts.DtoMappings.Settings;

public class AppSettingsDtoMapper : ILSCoreMapper<GeneralSettingsEntity, AppSettingsDto>
{
	public AppSettingsDto ToMapped(GeneralSettingsEntity source) =>
		new()
		{
			MarketDataType = source.MarketDataType,
			AutoReconnect = source.AutoReconnect,
			HeartbeatSeconds = source.HeartbeatSeconds,
			StopGatewayOnExit = source.StopGatewayOnExit,
		};
}
