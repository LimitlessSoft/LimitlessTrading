using LSCore.Mapper.Contracts;
using LT.Contracts.DtoMappings.Contracts;
using LT.Contracts.DtoMappings.Indicators;
using LT.Contracts.Dtos.Settings;
using LT.Contracts.Entities;

namespace LT.Contracts.DtoMappings.Settings;

public class WidgetPlacementEntityMapper : ILSCoreMapper<WidgetPlacementDto, WidgetPlacementEntity>
{
	public WidgetPlacementEntity ToMapped(WidgetPlacementDto source) =>
		new()
		{
			Id = source.Id,
			Tool = source.Tool,
			Dock = source.Dock,
			FloatX = source.FloatX,
			FloatY = source.FloatY,
			FloatWidth = source.FloatWidth,
			FloatHeight = source.FloatHeight,
			LinkGroup = source.LinkGroup,
			IsPinned = source.IsPinned,
			SideWidth = source.SideWidth,
			BottomHeight = source.BottomHeight,
			ChartTimeframe = source.ChartTimeframe,
			ChartLinked = source.ChartLinked,
			ChartRightMargin = source.ChartRightMargin,
			ChartIndicators = source.ChartIndicators is { } indicators
				? [.. indicators.Select(new IndicatorInstanceEntityMapper().ToMapped)]
				: null,
			NewsMode = source.NewsMode,
			NewsProviders = source.NewsProviders is { } providers ? [.. providers] : null,
			OptionQuickOrderQuantity = source.OptionQuickOrderQuantity,
			ChartContract = source.ChartContract is { } contract
				? new ContractEntityMapper().ToMapped(contract)
				: null,
		};
}
