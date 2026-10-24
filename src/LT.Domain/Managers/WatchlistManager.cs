using LSCore.Exceptions;
using LSCore.Mapper.Domain;
using LSCore.Validation.Contracts;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Entities;
using LT.Contracts.Enums.ValidationCodes;
using LT.Contracts.Helpers;
using LT.Contracts.Interfaces.IManagers;
using LT.Contracts.Interfaces.IRepositories;

namespace LT.Domain.Managers;

public class WatchlistManager(ISettingsRepository settingsRepository) : IWatchlistManager
{
	public IReadOnlyList<ContractDto> GetItems() =>
		settingsRepository.Get().Watchlist.ToMappedList<ContractEntity, ContractDto>();

	public void Add(ContractDto contract)
	{
		if (contract.ConId <= 0)
			throw new LSCoreBadRequestException(
				ContractValidationCodes.CTVC_004.GetValidationMessage()
			);

		var settings = settingsRepository.Get();
		if (settings.Watchlist.Any(x => x.ConId == contract.ConId))
			throw new LSCoreBadRequestException(
				WatchlistValidationCodes.WVC_001.GetValidationMessage(
					ContractHelpers.GetDisplayName(contract)
				)
			);
		if (settings.Watchlist.Count >= MarketDataConstants.WatchlistMaxItems)
			throw new LSCoreBadRequestException(
				WatchlistValidationCodes.WVC_002.GetValidationMessage(
					MarketDataConstants.WatchlistMaxItems
				)
			);

		settings.Watchlist.Add(contract.ToMapped<ContractDto, ContractEntity>());
		settingsRepository.Save(settings);
	}

	public void Remove(int conId)
	{
		var settings = settingsRepository.Get();
		if (settings.Watchlist.RemoveAll(x => x.ConId == conId) > 0)
			settingsRepository.Save(settings);
	}
}
