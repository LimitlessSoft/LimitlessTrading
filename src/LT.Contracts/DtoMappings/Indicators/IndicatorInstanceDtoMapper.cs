using LSCore.Mapper.Contracts;
using LT.Contracts.Dtos.Indicators;
using LT.Contracts.Entities;

namespace LT.Contracts.DtoMappings.Indicators;

public class IndicatorInstanceDtoMapper
	: ILSCoreMapper<IndicatorInstanceEntity, IndicatorInstanceDto>
{
	public IndicatorInstanceDto ToMapped(IndicatorInstanceEntity source) =>
		new()
		{
			Id = string.IsNullOrEmpty(source.Id) ? Guid.NewGuid().ToString("N") : source.Id,
			DefinitionId = source.DefinitionId,
			Inputs = new Dictionary<string, double>(source.Inputs ?? []),
			IsVisible = source.IsVisible,
			PlotColors = source.PlotColors is { } colors
				? new Dictionary<string, uint>(colors)
				: null,
		};
}
