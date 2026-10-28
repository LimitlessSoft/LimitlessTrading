using LSCore.DependencyInjection;
using LSCore.Mapper.Contracts;
using LSCore.Validation.Domain;
using LT.Contracts.DtoMappings.AccountPerformance;
using LT.Contracts.DtoMappings.ChartDrawings;
using LT.Contracts.DtoMappings.Charts;
using LT.Contracts.DtoMappings.Contracts;
using LT.Contracts.DtoMappings.Executions;
using LT.Contracts.DtoMappings.Flex;
using LT.Contracts.DtoMappings.Performance;
using LT.Contracts.DtoMappings.PriceAlerts;
using LT.Contracts.DtoMappings.Settings;
using LT.Contracts.DtoMappings.Watchlists;
using LT.Contracts.Dtos.AccountPerformance;
using LT.Contracts.Dtos.ChartDrawings;
using LT.Contracts.Dtos.Charts;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.Executions;
using LT.Contracts.Dtos.Flex;
using LT.Contracts.Dtos.Performance;
using LT.Contracts.Dtos.PriceAlerts;
using LT.Contracts.Dtos.Settings;
using LT.Contracts.Dtos.Watchlists;
using LT.Contracts.Entities;
using LT.Contracts.Requests.Calendar;
using LT.Contracts.Requests.ChartDrawings;
using LT.Contracts.Requests.ChartTemplates;
using LT.Contracts.Requests.Connection;
using LT.Contracts.Requests.Contracts;
using LT.Contracts.Requests.Flex;
using LT.Contracts.Requests.Gateway;
using LT.Contracts.Requests.Indicators;
using LT.Contracts.Requests.MarketDepth;
using LT.Contracts.Requests.News;
using LT.Contracts.Requests.Options;
using LT.Contracts.Requests.Orders;
using LT.Contracts.Requests.PriceAlerts;
using LT.Contracts.Requests.Settings;
using LT.Contracts.Requests.Watchlists;
using LT.Domain.Validators.Calendar;
using LT.Domain.Validators.ChartDrawings;
using LT.Domain.Validators.ChartTemplates;
using LT.Domain.Validators.Connection;
using LT.Domain.Validators.Contracts;
using LT.Domain.Validators.Flex;
using LT.Domain.Validators.Gateway;
using LT.Domain.Validators.Indicators;
using LT.Domain.Validators.MarketDepth;
using LT.Domain.Validators.News;
using LT.Domain.Validators.Options;
using LT.Domain.Validators.Orders;
using LT.Domain.Validators.PriceAlerts;
using LT.Domain.Validators.Settings;
using LT.Domain.Validators.Watchlists;
using LT.Ibkr.DtoMappings.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace LT.Tests.Fakes;

/// <summary>request.Validate() and ToMapped() resolve through LSCore's static container.</summary>
public static class LSCoreContainer
{
	private static readonly Lock Lock = new();

	public static void EnsureInitialized()
	{
		lock (Lock)
		{
			if (Container.ServiceProvider != null)
				return;

			Container.ServiceProvider = new ServiceCollection()
				.AddSingleton<LSCoreValidatorBase<ConnectRequest>, ConnectRequestValidator>()
				.AddSingleton<
					LSCoreValidatorBase<StartGatewayRequest>,
					StartGatewayRequestValidator
				>()
				.AddSingleton<
					LSCoreValidatorBase<ContractSearchRequest>,
					ContractSearchRequestValidator
				>()
				.AddSingleton<
					LSCoreValidatorBase<GetOptionChainRequest>,
					GetOptionChainRequestValidator
				>()
				.AddSingleton<LSCoreValidatorBase<PlaceOrderRequest>, PlaceOrderRequestValidator>()
				.AddSingleton<
					LSCoreValidatorBase<GetCalendarEventsRequest>,
					GetCalendarEventsRequestValidator
				>()
				.AddSingleton<
					LSCoreValidatorBase<SubscribeOrderBookRequest>,
					SubscribeOrderBookRequestValidator
				>()
				.AddSingleton<
					LSCoreValidatorBase<GetNewsHeadlinesRequest>,
					GetNewsHeadlinesRequestValidator
				>()
				.AddSingleton<
					LSCoreValidatorBase<SubscribeNewsRequest>,
					SubscribeNewsRequestValidator
				>()
				.AddSingleton<
					LSCoreValidatorBase<SaveAppSettingsRequest>,
					SaveAppSettingsRequestValidator
				>()
				.AddSingleton<
					LSCoreValidatorBase<ImportLayoutRequest>,
					ImportLayoutRequestValidator
				>()
				.AddSingleton<ILSCoreMapper<ContractEntity, ContractDto>, ContractDtoMapper>()
				.AddSingleton<ILSCoreMapper<WatchlistEntity, WatchlistDto>, WatchlistDtoMapper>()
				.AddSingleton<
					LSCoreValidatorBase<SaveWatchlistNameRequest>,
					SaveWatchlistNameRequestValidator
				>()
				.AddSingleton<
					ILSCoreMapper<WatchlistColumnEntity, WatchlistColumnDto>,
					WatchlistColumnDtoMapper
				>()
				.AddSingleton<
					LSCoreValidatorBase<SaveWatchlistColumnRequest>,
					SaveWatchlistColumnRequestValidator
				>()
				.AddSingleton<
					LSCoreValidatorBase<SetWatchlistCellRequest>,
					SetWatchlistCellRequestValidator
				>()
				.AddSingleton<
					LSCoreValidatorBase<ImportWatchlistRequest>,
					ImportWatchlistRequestValidator
				>()
				.AddSingleton<
					LSCoreValidatorBase<ImportWatchlistSymbolsRequest>,
					ImportWatchlistSymbolsRequestValidator
				>()
				.AddSingleton<ILSCoreMapper<ContractDto, ContractEntity>, ContractEntityMapper>()
				.AddSingleton<ILSCoreMapper<BarEntity, BarDto>, BarDtoMapper>()
				.AddSingleton<
					ILSCoreMapper<IBKR.Net.Contract, ContractDto>,
					IbkrContractDtoMapper
				>()
				.AddSingleton<ILSCoreMapper<BarDto, BarEntity>, BarEntityMapper>()
				.AddSingleton<ILSCoreMapper<LayoutEntity, LayoutDto>, LayoutDtoMapper>()
				.AddSingleton<ILSCoreMapper<LayoutDto, LayoutEntity>, LayoutEntityMapper>()
				.AddSingleton<
					ILSCoreMapper<GeneralSettingsEntity, AppSettingsDto>,
					AppSettingsDtoMapper
				>()
				.AddSingleton<
					ILSCoreMapper<AccountDaySnapshotEntity, AccountDaySnapshotDto>,
					AccountDaySnapshotDtoMapper
				>()
				.AddSingleton<
					ILSCoreMapper<CashMovementEntity, CashMovementDto>,
					CashMovementDtoMapper
				>()
				.AddSingleton<ILSCoreMapper<ExecutionDto, FillEntity>, FillEntityMapper>()
				.AddSingleton<ILSCoreMapper<FillEntity, ExecutionDto>, ExecutionDtoMapper>()
				.AddSingleton<ILSCoreMapper<PnlSampleEntity, PnlSampleDto>, PnlSampleDtoMapper>()
				.AddSingleton<ILSCoreMapper<FlexTradeDto, FillEntity>, FlexTradeFillEntityMapper>()
				.AddSingleton<
					ILSCoreMapper<FlexSettingsEntity, FlexSettingsDto>,
					FlexSettingsDtoMapper
				>()
				.AddSingleton<
					LSCoreValidatorBase<SaveFlexSettingsRequest>,
					SaveFlexSettingsRequestValidator
				>()
				.AddSingleton<
					LSCoreValidatorBase<UpdateIndicatorInputsRequest>,
					UpdateIndicatorInputsRequestValidator
				>()
				.AddSingleton<
					LSCoreValidatorBase<ComputeIndicatorsRequest>,
					ComputeIndicatorsRequestValidator
				>()
				.AddSingleton<
					LSCoreValidatorBase<SaveChartTemplateRequest>,
					SaveChartTemplateRequestValidator
				>()
				.AddSingleton<
					LSCoreValidatorBase<RenameChartTemplateRequest>,
					RenameChartTemplateRequestValidator
				>()
				.AddSingleton<
					LSCoreValidatorBase<ImportChartTemplatesRequest>,
					ImportChartTemplatesRequestValidator
				>()
				.AddSingleton<
					LSCoreValidatorBase<UpdateChartTemplateSettingsRequest>,
					UpdateChartTemplateSettingsRequestValidator
				>()
				.AddSingleton<ILSCoreMapper<PriceAlertEntity, PriceAlertDto>, PriceAlertDtoMapper>()
				.AddSingleton<
					LSCoreValidatorBase<CreatePriceAlertRequest>,
					CreatePriceAlertRequestValidator
				>()
				.AddSingleton<
					LSCoreValidatorBase<MovePriceAlertRequest>,
					MovePriceAlertRequestValidator
				>()
				.AddSingleton<
					ILSCoreMapper<ChartDrawingEntity, ChartDrawingDto>,
					ChartDrawingDtoMapper
				>()
				.AddSingleton<
					LSCoreValidatorBase<CreateChartDrawingRequest>,
					CreateChartDrawingRequestValidator
				>()
				.AddSingleton<
					LSCoreValidatorBase<UpdateChartDrawingRequest>,
					UpdateChartDrawingRequestValidator
				>()
				.BuildServiceProvider();
		}
	}
}
