using LSCore.DependencyInjection;
using LSCore.Mapper.Contracts;
using LSCore.Validation.Domain;
using LT.Contracts.DtoMappings.Charts;
using LT.Contracts.DtoMappings.Contracts;
using LT.Contracts.DtoMappings.Settings;
using LT.Contracts.Dtos.Account;
using LT.Contracts.Dtos.Charts;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.Settings;
using LT.Contracts.Entities;
using LT.Contracts.Requests.Connection;
using LT.Contracts.Requests.Contracts;
using LT.Contracts.Requests.Gateway;
using LT.Contracts.Requests.Options;
using LT.Contracts.Requests.Settings;
using LT.Domain.Validators.Connection;
using LT.Domain.Validators.Contracts;
using LT.Domain.Validators.Gateway;
using LT.Domain.Validators.Options;
using LT.Domain.Validators.Settings;
using LT.Ibkr.DtoMappings.Account;
using Microsoft.Extensions.DependencyInjection;
using TwsApi;

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
				.AddSingleton<
					LSCoreValidatorBase<SaveAppSettingsRequest>,
					SaveAppSettingsRequestValidator
				>()
				.AddSingleton<ILSCoreMapper<ContractEntity, ContractDto>, ContractDtoMapper>()
				.AddSingleton<ILSCoreMapper<ContractDto, ContractEntity>, ContractEntityMapper>()
				.AddSingleton<ILSCoreMapper<BarEntity, BarDto>, BarDtoMapper>()
				.AddSingleton<ILSCoreMapper<BarDto, BarEntity>, BarEntityMapper>()
				.AddSingleton<ILSCoreMapper<LayoutEntity, LayoutDto>, LayoutDtoMapper>()
				.AddSingleton<ILSCoreMapper<LayoutDto, LayoutEntity>, LayoutEntityMapper>()
				.AddSingleton<ILSCoreMapper<AccountValue, AccountValueDto>, AccountValueDtoMapper>()
				.AddSingleton<
					ILSCoreMapper<GeneralSettingsEntity, AppSettingsDto>,
					AppSettingsDtoMapper
				>()
				.BuildServiceProvider();
		}
	}
}
