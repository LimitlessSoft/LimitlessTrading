using LSCore.Exceptions;
using LSCore.Mapper.Domain;
using LSCore.Validation.Contracts;
using LSCore.Validation.Domain;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.PriceAlerts;
using LT.Contracts.Entities;
using LT.Contracts.Enums;
using LT.Contracts.Enums.ValidationCodes;
using LT.Contracts.Helpers;
using LT.Contracts.Interfaces.IManagers;
using LT.Contracts.Interfaces.IRepositories;
using LT.Contracts.Requests.PriceAlerts;

namespace LT.Domain.Managers;

/// <summary>
/// Singleton (Program.cs): holds the alerts read once from price-alerts.json and saves every
/// change, so every chart and the watcher (<see cref="WatchAsync"/>, run by PriceAlertService
/// while connected) see the same alerts and an alert fires only once.
/// </summary>
public class PriceAlertManager(
	IPriceAlertRepository repository,
	IMarketDataManager marketDataManager,
	IMessageLogManager messageLogManager,
	TimeProvider timeProvider
) : IPriceAlertManager
{
	private readonly Lock _lock = new();
	private List<PriceAlertEntity>? _alerts;

	public event Action? AlertsChanged;
	public event Action<PriceAlertDto>? AlertTriggered;

	// Read under _lock
	private List<PriceAlertEntity> Alerts => _alerts ??= repository.Get();

	public IReadOnlyList<PriceAlertDto> GetAlerts()
	{
		lock (_lock)
			return Alerts.ToMappedList<PriceAlertEntity, PriceAlertDto>();
	}

	public IReadOnlyList<PriceAlertDto> GetAlerts(int conId)
	{
		lock (_lock)
			return Alerts
				.Where(x => x.Contract.ConId == conId)
				.ToMappedList<PriceAlertEntity, PriceAlertDto>();
	}

	public PriceAlertDto Create(CreatePriceAlertRequest request)
	{
		request.Validate();
		var contract = request.Contract!;
		PriceAlertDto created;
		lock (_lock)
		{
			if (PriceAlertHelpers.GetLimitError(Alerts, contract.ConId) is { } error)
				throw new LSCoreBadRequestException(error);
			var alert = new PriceAlertEntity
			{
				Id = Guid.NewGuid().ToString("N"),
				Contract = contract.ToMapped<ContractDto, ContractEntity>(),
				Price = request.Price,
				Condition = PriceAlertHelpers.GetCondition(
					request.Price,
					request.CurrentPrice!.Value
				),
				Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
				CreatedAt = timeProvider.GetUtcNow(),
			};
			Alerts.Add(alert);
			repository.Save(Alerts);
			created = alert.ToMapped<PriceAlertEntity, PriceAlertDto>();
		}
		AlertsChanged?.Invoke();
		return created;
	}

	public PriceAlertDto Move(MovePriceAlertRequest request)
	{
		request.Validate();
		PriceAlertDto moved;
		lock (_lock)
		{
			var alert =
				Alerts.FirstOrDefault(x => x.Id == request.Id)
				?? throw new LSCoreBadRequestException(
					PriceAlertValidationCodes.PAVC_007.GetValidationMessage()
				);
			// Arming a triggered alert again counts it against the limits
			if (
				alert.TriggeredAt != null
				&& PriceAlertHelpers.GetLimitError(
					[.. Alerts.Where(x => x != alert)],
					alert.Contract.ConId
				)
					is { } error
			)
				throw new LSCoreBadRequestException(error);
			alert.Price = request.Price;
			alert.Condition = PriceAlertHelpers.GetCondition(
				request.Price,
				request.CurrentPrice!.Value
			);
			alert.TriggeredAt = null;
			alert.TriggerPrice = null;
			repository.Save(Alerts);
			moved = alert.ToMapped<PriceAlertEntity, PriceAlertDto>();
		}
		AlertsChanged?.Invoke();
		return moved;
	}

	public void Delete(string id)
	{
		lock (_lock)
		{
			if (Alerts.RemoveAll(x => x.Id == id) == 0)
				return;
			repository.Save(Alerts);
		}
		AlertsChanged?.Invoke();
	}

	public int DeleteTriggered(int conId)
	{
		int removed;
		lock (_lock)
		{
			removed = Alerts.RemoveAll(x => x.Contract.ConId == conId && x.TriggeredAt != null);
			if (removed == 0)
				return 0;
			repository.Save(Alerts);
		}
		AlertsChanged?.Invoke();
		return removed;
	}

	public IReadOnlyList<PriceAlertDto> OnPrice(int conId, double price)
	{
		if (!double.IsFinite(price) || price <= 0)
			return [];
		var fired = new List<PriceAlertDto>();
		lock (_lock)
		{
			var now = timeProvider.GetUtcNow();
			foreach (var alert in Alerts)
			{
				if (
					alert.TriggeredAt != null
					|| alert.Contract.ConId != conId
					|| !PriceAlertHelpers.IsCrossed(alert.Condition, alert.Price, price)
				)
					continue;
				alert.TriggeredAt = now;
				alert.TriggerPrice = price;
				fired.Add(alert.ToMapped<PriceAlertEntity, PriceAlertDto>());
			}
			if (fired.Count == 0)
				return fired;
			try
			{
				repository.Save(Alerts);
			}
			catch (Exception)
			{
				// Fired in memory either way: at worst it fires again after a restart
			}
		}
		foreach (var alert in fired)
		{
			messageLogManager.Add(
				MessageSeverity.Info,
				MessageLogConstants.SourceAlerts,
				PriceAlertHelpers.GetTriggeredText(alert)
			);
			AlertTriggered?.Invoke(alert);
		}
		AlertsChanged?.Invoke();
		return fired;
	}

	public async Task WatchAsync(CancellationToken cancellationToken)
	{
		// One stream per contract with an active alert, by ConId. Not disposed: cancelling is
		// enough, and the session token they are linked to ends with the connection
		var watches = new Dictionary<int, CancellationTokenSource>();
		using var changed = new SemaphoreSlim(0, 1);
		void OnChanged()
		{
			try
			{
				changed.Release();
			}
			catch (Exception ex) when (ex is SemaphoreFullException or ObjectDisposedException)
			{
				// A change is pending already, or watching has ended
			}
		}
		AlertsChanged += OnChanged;
		try
		{
			while (true)
			{
				Dictionary<int, ContractDto> contracts;
				lock (_lock)
				{
					var watched = PriceAlertHelpers.GetWatchedConIds(Alerts);
					contracts = Alerts
						.Where(x => watched.Contains(x.Contract.ConId))
						.DistinctBy(x => x.Contract.ConId)
						.ToDictionary(
							x => x.Contract.ConId,
							x => x.Contract.ToMapped<ContractEntity, ContractDto>()
						);
				}
				foreach (var conId in watches.Keys.Where(x => !contracts.ContainsKey(x)).ToList())
				{
					watches[conId].Cancel();
					watches.Remove(conId);
				}
				foreach (var (conId, contract) in contracts)
				{
					if (watches.ContainsKey(conId))
						continue;
					var watch = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
					watches[conId] = watch;
					var token = watch.Token;
					_ = Task.Run(() => WatchContractAsync(contract, token), CancellationToken.None);
				}
				await changed.WaitAsync(cancellationToken).ConfigureAwait(false);
			}
		}
		catch (OperationCanceledException) { }
		finally
		{
			AlertsChanged -= OnChanged;
			foreach (var watch in watches.Values)
				watch.Cancel();
		}
	}

	// Thread pool: one contract's quotes until cancelled, opened again after a failure
	private async Task WatchContractAsync(ContractDto contract, CancellationToken cancellationToken)
	{
		while (!cancellationToken.IsCancellationRequested)
		{
			try
			{
				await foreach (
					var quote in marketDataManager
						.SubscribeQuotesAsync(
							contract,
							cancellationToken,
							owner: MarketDataStreamOwners.PriceAlerts
						)
						.ConfigureAwait(false)
				)
					if (PriceAlertHelpers.GetPrice(quote, contract.SecurityType) is { } price)
						OnPrice(contract.ConId, price);
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				return;
			}
			catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
			{
				messageLogManager.Add(
					MessageSeverity.Warning,
					MessageLogConstants.SourceAlerts,
					$"{ContractHelpers.GetDisplayName(contract)}: alerts can't follow the price ({ex.Message}); trying again in {PriceAlertConstants.RetryDelay.TotalMinutes:0} min"
				);
			}
			try
			{
				await Task.Delay(PriceAlertConstants.RetryDelay, timeProvider, cancellationToken)
					.ConfigureAwait(false);
			}
			catch (OperationCanceledException)
			{
				return;
			}
		}
	}
}
