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
			FloatState = source.FloatState,
			FloatScreen = source.FloatScreen is { } screen
				? new WindowScreenEntityMapper().ToMapped(screen)
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
				? [.. indicators.Select(new IndicatorInstanceEntityMapper().ToMapped)]
				: null,
			ChartTemplateId = source.ChartTemplateId,
			ChartFollowTemplate = source.ChartFollowTemplate,
			ChartShowToolbox = source.ChartShowToolbox,
			ChartBarStyle = source.ChartBarStyle,
			ChartDrawingMode = source.ChartDrawingMode,
			ChartDrawingsLocked = source.ChartDrawingsLocked,
			ChartDrawingsHidden = source.ChartDrawingsHidden,
			ChartDrawingMagnet = source.ChartDrawingMagnet,
			ChartKeepDrawingTool = source.ChartKeepDrawingTool,
			NewsMode = source.NewsMode,
			NewsProviders = source.NewsProviders is { } providers ? [.. providers] : null,
			OptionQuickOrderQuantity = source.OptionQuickOrderQuantity,
			OptionHiddenColumns = source.OptionHiddenColumns is { } optionHidden
				? [.. optionHidden]
				: null,
			PerformanceShowCashMovements = source.PerformanceShowCashMovements,
			WatchlistId = source.WatchlistId,
			WatchlistHiddenColumns = source.WatchlistHiddenColumns is { } hiddenColumns
				? [.. hiddenColumns]
				: null,
			WatchlistHiddenCustomColumns = source.WatchlistHiddenCustomColumns is { } hiddenCustom
				? [.. hiddenCustom]
				: null,
			WatchlistSortBy = source.WatchlistSortBy,
			WatchlistSortCustomColumnId = source.WatchlistSortCustomColumnId,
			WatchlistSortDescending = source.WatchlistSortDescending,
			NoteId = source.NoteId,
			CalendarRange = source.CalendarRange,
			CalendarFilter = source.CalendarFilter,
			ChartContract = source.ChartContract is { } contract
				? new ContractEntityMapper().ToMapped(contract)
				: null,
		};
}
