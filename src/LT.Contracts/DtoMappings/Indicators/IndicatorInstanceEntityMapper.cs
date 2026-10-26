using LSCore.Mapper.Contracts;
using LT.Contracts.Dtos.Indicators;
using LT.Contracts.Entities;

namespace LT.Contracts.DtoMappings.Indicators;

public class IndicatorInstanceEntityMapper
	: ILSCoreMapper<IndicatorInstanceDto, IndicatorInstanceEntity>
{
	public IndicatorInstanceEntity ToMapped(IndicatorInstanceDto source) =>
		new()
		{
			Id = source.Id,
			DefinitionId = source.DefinitionId,
			Inputs = new Dictionary<string, double>(source.Inputs),
			IsVisible = source.IsVisible,
			PlotColors = source.PlotColors is { } colors
				? new Dictionary<string, uint>(colors)
				: null,
		};
}
