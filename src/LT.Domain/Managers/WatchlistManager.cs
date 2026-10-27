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

/// <summary>
/// Plain symbol lists, and file items without a conId, are looked up through
/// <see cref="IContractManager"/> one at a time, so the lookups queue behind IB's request pacing
/// instead of bursting.
/// </summary>
public class WatchlistManager(
	ISettingsRepository settingsRepository,
	IContractManager contractManager
) : IWatchlistManager
{
	public event Action? Changed;

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
		Changed?.Invoke();
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
		Changed?.Invoke();
	}

	public IReadOnlyList<ContractDto> GetItems(string watchlistId) =>
		(
			WatchlistHelpers.Find(WatchlistHelpers.LoadSettings(settingsRepository), watchlistId)
			?? throw new LSCoreBadRequestException(
				WatchlistValidationCodes.WVC_007.GetValidationMessage()
			)
		).Items.ToMappedList<ContractEntity, ContractDto>();

	public void Add(string watchlistId, ContractDto contract)
	{
		if (contract.ConId <= 0)
			throw new LSCoreBadRequestException(
				ContractValidationCodes.CTVC_004.GetValidationMessage()
			);

		var settings = WatchlistHelpers.LoadSettings(settingsRepository);
		var items = (
			WatchlistHelpers.Find(settings, watchlistId)
			?? throw new LSCoreBadRequestException(
				WatchlistValidationCodes.WVC_007.GetValidationMessage()
			)
		).Items;
		if (
			items.Any(x =>
				ContractHelpers.IsSameListing(x.ToMapped<ContractEntity, ContractDto>(), contract)
			)
		)
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
		Changed?.Invoke();
	}

	public void Remove(string watchlistId, ContractDto contract)
	{
		var settings = WatchlistHelpers.LoadSettings(settingsRepository);
		if (WatchlistHelpers.Find(settings, watchlistId) is not { } watchlist)
			return;
		var removed = watchlist.Items.RemoveAll(x =>
			ContractHelpers.IsSameListing(x.ToMapped<ContractEntity, ContractDto>(), contract)
		);
		var unpinned = watchlist.Pinned.RemoveAll(x => WatchlistHelpers.IsPinOf(x, contract));
		var cleared = watchlist.Columns.Sum(column =>
			column.Cells.RemoveAll(x => WatchlistHelpers.IsCellOf(x, contract))
		);
		if (removed + unpinned + cleared == 0)
			return;
		settingsRepository.Save(settings);
		Changed?.Invoke();
	}

	public IReadOnlyList<ContractDto> GetPinned(string watchlistId)
	{
		var watchlist =
			WatchlistHelpers.Find(WatchlistHelpers.LoadSettings(settingsRepository), watchlistId)
			?? throw new LSCoreBadRequestException(
				WatchlistValidationCodes.WVC_007.GetValidationMessage()
			);
		var items = watchlist.Items.ToMappedList<ContractEntity, ContractDto>();
		return watchlist
			.Pinned.Select(pin => items.FirstOrDefault(x => WatchlistHelpers.IsPinOf(pin, x)))
			.OfType<ContractDto>()
			.ToList();
	}

	public void Pin(string watchlistId, ContractDto contract)
	{
		var settings = WatchlistHelpers.LoadSettings(settingsRepository);
		var watchlist =
			WatchlistHelpers.Find(settings, watchlistId)
			?? throw new LSCoreBadRequestException(
				WatchlistValidationCodes.WVC_007.GetValidationMessage()
			);
		if (
			!watchlist.Items.Any(x =>
				ContractHelpers.IsSameListing(x.ToMapped<ContractEntity, ContractDto>(), contract)
			)
		)
			throw new LSCoreBadRequestException(
				WatchlistValidationCodes.WVC_008.GetValidationMessage(
					ContractHelpers.GetDisplayName(contract)
				)
			);
		if (watchlist.Pinned.Any(x => WatchlistHelpers.IsPinOf(x, contract)))
			return;

		watchlist.Pinned.Add(WatchlistHelpers.CreatePin(contract));
		settingsRepository.Save(settings);
		Changed?.Invoke();
	}

	public void Unpin(string watchlistId, ContractDto contract)
	{
		var settings = WatchlistHelpers.LoadSettings(settingsRepository);
		if (
			WatchlistHelpers.Find(settings, watchlistId) is not { } watchlist
			|| watchlist.Pinned.RemoveAll(x => WatchlistHelpers.IsPinOf(x, contract)) == 0
		)
			return;
		settingsRepository.Save(settings);
		Changed?.Invoke();
	}

	public IReadOnlyList<WatchlistColumnDto> GetColumns(string watchlistId) =>
		(
			WatchlistHelpers.Find(WatchlistHelpers.LoadSettings(settingsRepository), watchlistId)
			?? throw new LSCoreBadRequestException(
				WatchlistValidationCodes.WVC_007.GetValidationMessage()
			)
		).Columns.ToMappedList<WatchlistColumnEntity, WatchlistColumnDto>();

	public WatchlistColumnDto SaveColumn(SaveWatchlistColumnRequest request)
	{
		request.Validate();
		var name = request.Name.Trim();
		var settings = WatchlistHelpers.LoadSettings(settingsRepository);
		var watchlist =
			WatchlistHelpers.Find(settings, request.WatchlistId)
			?? throw new LSCoreBadRequestException(
				WatchlistValidationCodes.WVC_007.GetValidationMessage()
			);
		if (
			watchlist.Columns.Any(x =>
				x.Id != request.ColumnId
				&& string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase)
			)
		)
			throw new LSCoreBadRequestException(
				WatchlistValidationCodes.WVC_011.GetValidationMessage(name)
			);

		WatchlistColumnEntity column;
		if (request.ColumnId is null)
		{
			if (watchlist.Columns.Count >= WatchlistConstants.MaxCustomColumns)
				throw new LSCoreBadRequestException(
					WatchlistValidationCodes.WVC_012.GetValidationMessage(
						WatchlistConstants.MaxCustomColumns
					)
				);
			column = new WatchlistColumnEntity { Name = name };
			watchlist.Columns.Add(column);
		}
		else
		{
			column =
				watchlist.Columns.FirstOrDefault(x => x.Id == request.ColumnId)
				?? throw new LSCoreBadRequestException(
					WatchlistValidationCodes.WVC_013.GetValidationMessage()
				);
			column.Name = name;
		}
		settingsRepository.Save(settings);
		Changed?.Invoke();
		return column.ToMapped<WatchlistColumnEntity, WatchlistColumnDto>();
	}

	public string GetNewColumnName(string watchlistId) =>
		WatchlistHelpers.GetNewColumnName(GetColumns(watchlistId).Select(x => x.Name));

	public void DeleteColumn(string watchlistId, string columnId)
	{
		var settings = WatchlistHelpers.LoadSettings(settingsRepository);
		if (
			WatchlistHelpers.Find(settings, watchlistId) is not { } watchlist
			|| watchlist.Columns.RemoveAll(x => x.Id == columnId) == 0
		)
			return;
		settingsRepository.Save(settings);
		Changed?.Invoke();
	}

	public void SetCell(SetWatchlistCellRequest request)
	{
		request.Validate();
		var value = request.Value?.Trim() ?? string.Empty;
		var settings = WatchlistHelpers.LoadSettings(settingsRepository);
		var watchlist =
			WatchlistHelpers.Find(settings, request.WatchlistId)
			?? throw new LSCoreBadRequestException(
				WatchlistValidationCodes.WVC_007.GetValidationMessage()
			);
		var column =
			watchlist.Columns.FirstOrDefault(x => x.Id == request.ColumnId)
			?? throw new LSCoreBadRequestException(
				WatchlistValidationCodes.WVC_013.GetValidationMessage()
			);
		if (
			!watchlist.Items.Any(x =>
				ContractHelpers.IsSameListing(
					x.ToMapped<ContractEntity, ContractDto>(),
					request.Contract
				)
			)
		)
			throw new LSCoreBadRequestException(
				WatchlistValidationCodes.WVC_008.GetValidationMessage(
					ContractHelpers.GetDisplayName(request.Contract)
				)
			);

		var cell = column.Cells.FirstOrDefault(x => WatchlistHelpers.IsCellOf(x, request.Contract));
		if ((cell?.Value ?? string.Empty) == value)
			return;
		if (value.Length == 0)
			column.Cells.Remove(cell!);
		else if (cell != null)
			cell.Value = value;
		else
			column.Cells.Add(WatchlistHelpers.CreateCell(request.Contract, value));
		settingsRepository.Save(settings);
		Changed?.Invoke();
	}

	public string Export(string watchlistId) =>
		WatchlistFileHelpers.Serialize(
			WatchlistHelpers.Find(WatchlistHelpers.LoadSettings(settingsRepository), watchlistId)
				?? throw new LSCoreBadRequestException(
					WatchlistValidationCodes.WVC_007.GetValidationMessage()
				)
		);

	public async Task<WatchlistImportResultDto> ImportAsync(
		string content,
		string? fileName,
		CancellationToken cancellationToken = default
	)
	{
		if (content.Length > WatchlistConstants.MaxFileLength)
			throw new LSCoreBadRequestException(
				WatchlistValidationCodes.WVC_017.GetValidationMessage(
					WatchlistConstants.MaxFileLength / (1024 * 1024)
				)
			);
		if (!WatchlistFileHelpers.IsText(content))
			throw new LSCoreBadRequestException(
				WatchlistValidationCodes.WVC_015.GetValidationMessage()
			);

		WatchlistEntity watchlist;
		var notFound = new List<string>();
		if (WatchlistFileHelpers.IsJson(content))
		{
			var file =
				WatchlistFileHelpers.Parse(content)
				?? throw new LSCoreBadRequestException(
					WatchlistValidationCodes.WVC_015.GetValidationMessage()
				);
			var request = WatchlistFileHelpers.ToRequest(file);
			request.Validate();

			// Items without a conId are looked up by their fields, one at a time (IB's pacing);
			// those IB doesn't know, or that stay ambiguous, are left out and returned
			var items = new List<WatchlistImportItemDto>();
			for (var i = 0; i < request.Items.Count; i++)
			{
				var item = request.Items[i];
				if (item.Contract is { ConId: > 0 })
				{
					items.Add(item);
					continue;
				}
				ContractDto? contract = null;
				try
				{
					contract = ContractHelpers.PickMatch(
						item.Contract!,
						await contractManager.FindAsync(item.Contract!, cancellationToken)
					);
				}
				// IB answers an unknown contract with an error
				catch (Exception ex) when (ex is not OperationCanceledException) { }
				if (contract == null)
				{
					notFound.Add(WatchlistFileHelpers.DescribeItem(item.Contract, i));
					continue;
				}
				// Its pin and values follow it: they are matched by the resolved listing
				item.Contract = contract;
				items.Add(item);
			}
			if (items.Count == 0)
				throw new LSCoreBadRequestException(
					WatchlistValidationCodes.WVC_019.GetValidationMessage(
						WatchlistFileHelpers.JoinSymbols(notFound)
					)
				);
			request.Items = items;

			watchlist = WatchlistFileHelpers.ToEntity(
				request,
				WatchlistFileHelpers.GetImportName(request.Name, fileName)
			);
		}
		else
		{
			var request = new ImportWatchlistSymbolsRequest
			{
				Symbols = WatchlistFileHelpers.ParseSymbols(content),
			};
			request.Validate();

			// One symbol at a time: every lookup queues behind IB's request pacing (and is cached)
			var contracts = new List<ContractDto>();
			foreach (var symbol in request.Symbols)
			{
				ContractDto? contract = null;
				try
				{
					contract = (await contractManager.SearchSymbolAsync(symbol, cancellationToken))
						.FirstOrDefault()
						?.Contract;
				}
				// IB answers an unknown symbol with an error for every type
				catch (Exception ex) when (ex is not OperationCanceledException) { }
				if (contract is not { ConId: > 0 })
					notFound.Add(symbol);
				else if (!contracts.Any(x => ContractHelpers.IsSameListing(x, contract)))
					contracts.Add(contract);
			}
			if (contracts.Count == 0)
				throw new LSCoreBadRequestException(
					WatchlistValidationCodes.WVC_019.GetValidationMessage(
						WatchlistFileHelpers.JoinSymbols(notFound)
					)
				);
			watchlist = new WatchlistEntity
			{
				Name = WatchlistFileHelpers.GetImportName(null, fileName),
				Items = [.. contracts.Select(x => x.ToMapped<ContractDto, ContractEntity>())],
			};
		}

		// Read after the lookups: the watchlists may have changed meanwhile
		var settings = WatchlistHelpers.LoadSettings(settingsRepository);
		watchlist.Name = WatchlistHelpers.GetFreeName(
			watchlist.Name,
			settings.Watchlists.Select(x => x.Name)
		);
		settings.Watchlists.Add(watchlist);
		settings.ActiveWatchlistId = watchlist.Id;
		settingsRepository.Save(settings);
		Changed?.Invoke();
		return new WatchlistImportResultDto
		{
			Watchlist = watchlist.ToMapped<WatchlistEntity, WatchlistDto>(),
			NotFound = notFound,
		};
	}
}
