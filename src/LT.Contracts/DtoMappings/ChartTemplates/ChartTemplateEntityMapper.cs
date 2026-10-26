using LSCore.Mapper.Contracts;
using LT.Contracts.DtoMappings.Indicators;
using LT.Contracts.Dtos.ChartTemplates;
using LT.Contracts.Entities;

namespace LT.Contracts.DtoMappings.ChartTemplates;

public class ChartTemplateEntityMapper : ILSCoreMapper<ChartTemplateDto, ChartTemplateEntity>
{
	public ChartTemplateEntity ToMapped(ChartTemplateDto source) =>
		new()
		{
			Id = source.Id,
			Name = source.Name,
			Version = source.Version,
			Settings = new ChartTemplateSettingsEntity
			{
				Timeframe = source.Settings.Timeframe,
				Period = source.Settings.Period,
				RightMargin = source.Settings.RightMargin,
				Indicators = source.Settings.Indicators is { } indicators
					? [.. indicators.Select(new IndicatorInstanceEntityMapper().ToMapped)]
					: null,
			},
		};
}
