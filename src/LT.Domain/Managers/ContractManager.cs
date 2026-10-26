using LSCore.Exceptions;
using LSCore.Mapper.Domain;
using LSCore.Validation.Contracts;
using LSCore.Validation.Domain;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Entities;
using LT.Contracts.Enums;
using LT.Contracts.Enums.ValidationCodes;
using LT.Contracts.Helpers;
using LT.Contracts.Interfaces.IApiClients;
using LT.Contracts.Interfaces.IManagers;
using LT.Contracts.Interfaces.IRepositories;
using LT.Contracts.Requests.Contracts;

namespace LT.Domain.Managers;

/// <summary>Searches are cached (CacheType.Contracts) for days: contract definitions barely change.</summary>
public class ContractManager(
	IIbkrApiClient ibkrApiClient,
	ISettingsRepository settingsRepository,
	ICacheManager cacheManager
) : IContractManager
{
	public async Task<IReadOnlyList<ContractDetailsDto>> SearchAsync(
		ContractSearchRequest request,
		CancellationToken cancellationToken = default
	)
	{
		request.Validate();

		// Stocks resolve once via SMART (primary exchange is reported); other types list every venue
		var exchange =
			!string.IsNullOrWhiteSpace(request.Exchange)
				? request.Exchange.Trim().ToUpperInvariant()
			: request.SecurityType == MarketDataConstants.StockType
				? MarketDataConstants.DefaultExchange
			: string.Empty;

		var query = new ContractDto
		{
			Symbol = request.Symbol.Trim().ToUpperInvariant(),
			SecurityType = request.SecurityType,
			Exchange = exchange,
			Currency = request.Currency?.Trim().ToUpperInvariant() ?? string.Empty,
		};

		// An empty answer may be a hiccup; it is never kept for days
		var cached = await cacheManager.GetOrFetchAsync(
			CacheType.Contracts,
			ContractHelpers.GetSearchCacheKey(query),
			CacheConstants.ContractTtl,
			async ct =>
				(await ibkrApiClient.SearchContractsAsync(query, ct))
					.OrderBy(x => x.Contract.Symbol)
					.ThenBy(x => x.Contract.Currency != MarketDataConstants.PreferredCurrency)
					.ThenBy(x => x.Contract.LastTradeDateOrContractMonth)
					.ThenBy(x => x.Contract.Currency)
					.ToList(),
			cancellationToken,
			x => x.Count > 0
		);
		return cached.Value;
	}

	public async Task<IReadOnlyList<ContractDetailsDto>> SearchSymbolAsync(
		string symbol,
		CancellationToken cancellationToken = default
	)
	{
		// The whole answer is cached too: types without the symbol fail and would be asked again
		var cached = await cacheManager.GetOrFetchAsync(
			CacheType.Contracts,
			ContractHelpers.GetSymbolSearchCacheKey(symbol),
			CacheConstants.ContractTtl,
			async ct =>
			{
				var searches = await Task.WhenAll(
					MarketDataConstants.SymbolSearchTypes.Select(async type =>
					{
						try
						{
							var results = await SearchAsync(
								new ContractSearchRequest { Symbol = symbol, SecurityType = type },
								ct
							);
							return (Results: results, Error: (Exception?)null);
						}
						catch (Exception ex) when (ex is not OperationCanceledException)
						{
							return (Results: (IReadOnlyList<ContractDetailsDto>)[], Error: ex);
						}
					})
				);

				// A type without that symbol fails ("no security definition"); only all failing is an error
				if (searches.All(x => x.Error != null))
					throw searches[0].Error!;
				return searches.SelectMany(x => x.Results).ToList();
			},
			cancellationToken,
			x => x.Count > 0
		);
		return cached.Value;
	}

	public async Task<ContractDto> ResolveTickerAsync(
		string ticker,
		CancellationToken cancellationToken = default
	)
	{
		var symbol = ticker.Trim().ToUpperInvariant();
		var results = await SearchSymbolAsync(symbol, cancellationToken);
		var contract =
			results.FirstOrDefault()?.Contract
			?? throw new LSCoreBadRequestException(
				ContractValidationCodes.CTVC_005.GetValidationMessage(symbol)
			);
		AddRecentContract(contract);
		return contract;
	}

	public IReadOnlyList<ContractDto> GetRecentContracts() =>
		settingsRepository.Get().RecentContracts.ToMappedList<ContractEntity, ContractDto>();

	public void AddRecentContract(ContractDto contract)
	{
		var settings = settingsRepository.Get();
		settings.RecentContracts = ContractHelpers
			.PushRecent(
				settings.RecentContracts.ToMappedList<ContractEntity, ContractDto>(),
				contract,
				MarketDataConstants.RecentContractsMax
			)
			.Select(x => x.ToMapped<ContractDto, ContractEntity>())
			.ToList();
		settingsRepository.Save(settings);
	}
}
