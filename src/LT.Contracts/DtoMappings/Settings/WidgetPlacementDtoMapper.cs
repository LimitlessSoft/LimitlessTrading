using LSCore.Mapper.Contracts;
using LT.Contracts.DtoMappings.Contracts;
using LT.Contracts.DtoMappings.Indicators;
using LT.Contracts.Dtos.Settings;
using LT.Contracts.Entities;

namespace LT.Contracts.DtoMappings.Settings;

public class WidgetPlacementDtoMapper : ILSCoreMapper<WidgetPlacementEntity, WidgetPlacementDto>
{
	public WidgetPlacementDto ToMapped(WidgetPlacementEntity source) =>
		new()
		{
			Id = source.Id,
			Tool = source.Tool,
			Dock = source.Dock,
			FloatX = source.FloatX,
			FloatY = source.FloatY,
			FloatWidth = source.FloatWidth,
			FloatHeight = source.FloatHeight,
			FloatState = source.FloatState,
			FloatScreen = source.FloatScreen is { } screen
				? new WindowScreenDtoMapper().ToMapped(screen)
				: null,
			LinkGroup = source.LinkGroup,
			IsPinned = source.IsPinned,
			SideWidth = source.SideWidth,
			BottomHeight = source.BottomHeight,
			ChartTimeframe = source.ChartTimeframe,
			ChartPeriod = source.ChartPeriod,
			ChartOutsideRegularHours = source.ChartOutsideRegularHours,
			ChartLinked = source.ChartLinked,
			ChartRightMargin = source.ChartRightMargin,
			ChartIndicators = source.ChartIndicators is { } indicators
				? [.. indicators.Select(new IndicatorInstanceDtoMapper().ToMapped)]
				: null,
			ChartTemplateId = source.ChartTemplateId,
			ChartFollowTemplate = source.ChartFollowTemplate,
			ChartShowToolbox = source.ChartShowToolbox,
			NewsMode = source.NewsMode,
			NewsProviders = source.NewsProviders is { } providers ? [.. providers] : null,
			OptionQuickOrderQuantity = source.OptionQuickOrderQuantity,
			PerformanceShowCashMovements = source.PerformanceShowCashMovements,
			WatchlistId = source.WatchlistId,
			NoteId = source.NoteId,
			ChartContract = source.ChartContract is { } contract
				? new ContractDtoMapper().ToMapped(contract)
				: null,
		};
}
