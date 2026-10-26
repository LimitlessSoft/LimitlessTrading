using LSCore.Mapper.Contracts;
using LT.Contracts.Dtos.Indicators;
using LT.Contracts.Enums;
using LT.Indicators.Sdk;

namespace LT.Indicators.Scripting.DtoMappings;

public class IndicatorInputDtoMapper : ILSCoreMapper<InputInfo, IndicatorInputDto>
{
	public IndicatorInputDto ToMapped(InputInfo source) =>
		new()
		{
			Name = source.Name,
			Type = source.Kind switch
			{
				InputKind.Int => IndicatorInputType.Int,
				InputKind.Bool => IndicatorInputType.Bool,
				_ => IndicatorInputType.Double,
			},
			DefaultValue = source.DefaultValue,
			Min = source.Min,
			Max = source.Max,
			Step = source.Step,
		};
}
