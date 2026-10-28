using LT.Contracts.Constants;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.Fundamentals;
using LT.Contracts.Entities;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;
using LT.Contracts.Interfaces.IApiClients;
using LT.Contracts.Interfaces.IManagers;
using LT.Contracts.Interfaces.IRepositories;

namespace LT.Domain.Managers;

/// <summary>
/// Shares outstanding from SEC filings: ticker → CIK (the SEC's ticker file, loaded once per
/// run), then the company's dei:EntityCommonStockSharesOutstanding, falling back to its full
/// facts (a count as of a date, then the latest weighted average), then to the per-class counts
/// on its latest 10-K / 10-Q's cover page (Berkshire, Visa). Results, "nothing usable" included, are cached on disk (<see cref="MarketCapHelpers.IsFresh"/>).
/// One lookup per ticker at a time; after a failure none starts for
/// <see cref="SecConstants.FailureBackoff"/> and the failure is logged once. Singleton.
/// </summary>
public class SharesOutstandingManager(
	ISecApiClient secApiClient,
	ISharesOutstandingRepository repository,
	IMessageLogManager messageLogManager,
	TimeProvider timeProvider
) : ISharesOutstandingManager
{
	private readonly Lock _lock = new();
	private readonly Dictionary<string, Task<long?>> _inFlight = new(
		StringComparer.OrdinalIgnoreCase
	);
	private Task<IReadOnlyDictionary<string, int>>? _ciks;
	private DateTimeOffset _retryAfter = DateTimeOffset.MinValue;

	public async Task<long?> GetSharesOutstandingAsync(
		ContractDto contract,
		CancellationToken cancellationToken = default
	)
	{
		if (!MarketCapHelpers.IsEligible(contract))
			return null;

		var ticker = MarketCapHelpers.GetSecTicker(contract.Symbol);
		var cached = repository.Get(ticker);
		var now = timeProvider.GetUtcNow();
		if (cached != null && MarketCapHelpers.IsFresh(cached, now))
			return cached.Shares;

		Task<long?> lookup;
		lock (_lock)
		{
			if (now < _retryAfter)
				return cached?.Shares;
			if (!_inFlight.TryGetValue(ticker, out lookup!))
			{
				// Not tied to one caller: others may be waiting on the same lookup
				lookup = Task.Run(() => LookUpAsync(ticker, cached));
				_inFlight[ticker] = lookup;
			}
		}
		return await lookup.WaitAsync(cancellationToken);
	}

	private async Task<long?> LookUpAsync(string ticker, SharesOutstandingEntity? cached)
	{
		try
		{
			var ciks = await GetCiksAsync();
			var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
			SharesOutstandingDto? found = null;
			if (ciks.TryGetValue(ticker, out var cik))
			{
				found = MarketCapHelpers.PickSharesOutstanding(
					await secApiClient.GetSharesOutstandingFactsAsync(cik) ?? [],
					today
				);
				if (found == null)
				{
					var facts = await secApiClient.GetCompanyShareFactsAsync(cik) ?? [];
					found =
						MarketCapHelpers.PickSharesOutstanding(facts, today)
						?? MarketCapHelpers.PickWeightedAverageShares(facts, today);
				}
				found ??= MarketCapHelpers.PickFilingShares(
					await secApiClient.GetLatestFilingShareFactsAsync(cik) ?? [],
					ticker,
					today
				);
			}

			repository.Save(
				ticker,
				new SharesOutstandingEntity
				{
					Shares = found?.Shares,
					AsOf = found?.AsOf,
					FetchedAt = timeProvider.GetUtcNow(),
					LookupVersion = SecConstants.SharesLookupVersion,
				}
			);
			return found?.Shares;
		}
		catch (Exception ex)
		{
			var log = false;
			lock (_lock)
			{
				var now = timeProvider.GetUtcNow();
				if (now >= _retryAfter)
				{
					_retryAfter = now + SecConstants.FailureBackoff;
					log = true;
				}
			}
			if (log)
				messageLogManager.Add(
					MessageSeverity.Warning,
					MessageLogConstants.SourceSec,
					$"Shares outstanding for market cap unavailable ({ticker}): {ex.Message}"
				);
			return cached?.Shares;
		}
		finally
		{
			lock (_lock)
				_inFlight.Remove(ticker);
		}
	}

	// The ticker file once per run; a failed load is retried by the next lookup
	private Task<IReadOnlyDictionary<string, int>> GetCiksAsync()
	{
		lock (_lock)
		{
			if (_ciks == null || _ciks.IsFaulted || _ciks.IsCanceled)
				_ciks = secApiClient.GetTickerCiksAsync();
			return _ciks;
		}
	}
}
