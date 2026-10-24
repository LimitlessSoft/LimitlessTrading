using LSCore.Mapper.Contracts;
using LT.Contracts.Dtos.Charts;
using TwsApi;

namespace LT.Ibkr.DtoMappings.Charts;

public class RealtimeBarDtoMapper : ILSCoreMapper<RealtimeBar, BarDto>
{
	public BarDto ToMapped(RealtimeBar source) =>
		new()
		{
			Time = DateTimeOffset.FromUnixTimeSeconds(source.Time),
			Open = source.Open,
			High = source.High,
			Low = source.Low,
			Close = source.Close,
			Volume = source.Volume < 0 ? 0 : source.Volume,
		};
}
