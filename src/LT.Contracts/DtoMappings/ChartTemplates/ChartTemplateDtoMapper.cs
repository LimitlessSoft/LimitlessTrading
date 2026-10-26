using LSCore.Mapper.Contracts;
using LT.Contracts.DtoMappings.Indicators;
using LT.Contracts.Dtos.ChartTemplates;
using LT.Contracts.Entities;

namespace LT.Contracts.DtoMappings.ChartTemplates;

/// <summary>Tolerates what a hand-edited or newer file may leave out (nulls stay "not set").</summary>
public class ChartTemplateDtoMapper : ILSCoreMapper<ChartTemplateEntity, ChartTemplateDto>
{
	public ChartTemplateDto ToMapped(ChartTemplateEntity source) =>
		new()
		{
			Id = source.Id ?? string.Empty,
			Name = source.Name ?? string.Empty,
			Version = source.Version,
			Settings = source.Settings is { } settings
				? new ChartTemplateSettingsDto
				{
					Timeframe = settings.Timeframe,
					Period = settings.Period,
					RightMargin = settings.RightMargin,
					Indicators = settings.Indicators is { } indicators
						?
						[
							.. indicators
								.Where(x => x != null)
								.Select(new IndicatorInstanceDtoMapper().ToMapped),
						]
						: null,
				}
				: new ChartTemplateSettingsDto(),
		};
}
