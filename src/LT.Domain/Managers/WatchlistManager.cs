using LSCore.Exceptions;
using LSCore.Mapper.Domain;
using LSCore.Validation.Contracts;
using LSCore.Validation.Domain;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.Watchlists;
using LT.Contracts.Entities;
using LT.Contracts.Enums.ValidationCodes;
using LT.Contracts.Helpers;
using LT.Contracts.Interfaces.IManagers;
using LT.Contracts.Interfaces.IRepositories;
using LT.Contracts.Requests.Watchlists;

namespace LT.Domain.Managers;

public class WatchlistManager(ISettingsRepository settingsRepository) : IWatchlistManager
{
	public IReadOnlyList<WatchlistDto> GetWatchlists() =>
		WatchlistHelpers
			.LoadSettings(settingsRepository)
			.Watchlists.ToMappedList<WatchlistEntity, WatchlistDto>();

	public WatchlistDto GetActive() =>
		WatchlistHelpers
			.GetActive(WatchlistHelpers.LoadSettings(settingsRepository))
			.ToMapped<WatchlistEntity, WatchlistDto>();

	public void SetActive(string id)
	{
		var settings = WatchlistHelpers.LoadSettings(settingsRepository);
		if (settings.Watchlists.All(x => x.Id != id))
			throw new LSCoreBadRequestException(
				WatchlistValidationCodes.WVC_007.GetValidationMessage()
			);
		settings.ActiveWatchlistId = id;
		settingsRepository.Save(settings);
	}

	public WatchlistDto SaveName(SaveWatchlistNameRequest request)
	{
		request.Validate();
		var name = request.Name.Trim();
		var settings = WatchlistHelpers.LoadSettings(settingsRepository);
		if (
			settings.Watchlists.Any(x =>
				x.Id != request.Id
				&& string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase)
			)
		)
			throw new LSCoreBadRequestException(
				WatchlistValidationCodes.WVC_005.GetValidationMessage(name)
			);

		WatchlistEntity watchlist;
		if (request.Id is null)
		{
			watchlist = new WatchlistEntity { Name = name };
			settings.Watchlists.Add(watchlist);
			settings.ActiveWatchlistId = watchlist.Id;
		}
		else
		{
			watchlist =
				settings.Watchlists.FirstOrDefault(x => x.Id == request.Id)
				?? throw new LSCoreBadRequestException(
					WatchlistValidationCodes.WVC_007.GetValidationMessage()
				);
			watchlist.Name = name;
		}
		settingsRepository.Save(settings);
		return watchlist.ToMapped<WatchlistEntity, WatchlistDto>();
	}

	public string GetNewName() =>
		WatchlistHelpers.GetNewName(
			WatchlistHelpers.LoadSettings(settingsRepository).Watchlists.Select(x => x.Name)
		);

	public void Delete(string id)
	{
		var settings = WatchlistHelpers.LoadSettings(settingsRepository);
		if (settings.Watchlists.Count <= 1)
			throw new LSCoreBadRequestException(
				WatchlistValidationCodes.WVC_006.GetValidationMessage()
			);
		if (settings.Watchlists.RemoveAll(x => x.Id == id) == 0)
			return;
		if (settings.ActiveWatchlistId == id)
			settings.ActiveWatchlistId = settings.Watchlists[0].Id;
		settingsRepository.Save(settings);
	}

	public IReadOnlyList<ContractDto> GetItems() =>
		WatchlistHelpers
			.GetActive(WatchlistHelpers.LoadSettings(settingsRepository))
			.Items.ToMappedList<ContractEntity, ContractDto>();

	public void Add(ContractDto contract)
	{
		if (contract.ConId <= 0)
			throw new LSCoreBadRequestException(
				ContractValidationCodes.CTVC_004.GetValidationMessage()
			);

		var settings = WatchlistHelpers.LoadSettings(settingsRepository);
		var items = WatchlistHelpers.GetActive(settings).Items;
		if (items.Any(x => x.ConId == contract.ConId))
			throw new LSCoreBadRequestException(
				WatchlistValidationCodes.WVC_001.GetValidationMessage(
					ContractHelpers.GetDisplayName(contract)
				)
			);
		if (items.Count >= MarketDataConstants.WatchlistMaxItems)
			throw new LSCoreBadRequestException(
				WatchlistValidationCodes.WVC_002.GetValidationMessage(
					MarketDataConstants.WatchlistMaxItems
				)
			);

		items.Add(contract.ToMapped<ContractDto, ContractEntity>());
		settingsRepository.Save(settings);
	}

	public void Remove(int conId)
	{
		var settings = WatchlistHelpers.LoadSettings(settingsRepository);
		if (WatchlistHelpers.GetActive(settings).Items.RemoveAll(x => x.ConId == conId) > 0)
			settingsRepository.Save(settings);
	}
}
