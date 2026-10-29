using LT.Contracts.Constants;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.Fundamentals;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;
using LT.Contracts.Interfaces.IApiClients;
using LT.Contracts.Interfaces.IManagers;

namespace LT.Domain.Managers;

/// <summary>
/// Financial statements from SEC filings: ticker → CIK (the SEC's ticker file, loaded once per
/// run), then the company's companyfacts, read into periods by
/// <see cref="FinancialsHelpers.Build"/>. Results are kept in memory per SEC ticker
/// (<see cref="FinancialsConstants.FoundTtl"/>, "no statements" for
/// <see cref="FinancialsConstants.NotFiledTtl"/>); one lookup per ticker at a time; after a
/// failure none starts for <see cref="SecConstants.FailureBackoff"/> and the failure is logged
/// once, an earlier result is kept meanwhile. Singleton.
/// </summary>
public class FinancialsManager(
	ISecApiClient secApiClient,
	IMessageLogManager messageLogManager,
	TimeProvider timeProvider
) : IFinancialsManager
{
	private readonly Lock _lock = new();
	private readonly Dictionary<string, (FinancialsDto Financials, DateTimeOffset At)> _cache = new(
		StringComparer.OrdinalIgnoreCase
	);
	private readonly Dictionary<string, Task<FinancialsDto>> _inFlight = new(
		StringComparer.OrdinalIgnoreCase
	);
	private Task<IReadOnlyDictionary<string, int>>? _ciks;
	private DateTimeOffset _retryAfter = DateTimeOffset.MinValue;

	public async Task<FinancialsDto> GetFinancialsAsync(
		ContractDto contract,
		CancellationToken cancellationToken = default
	)
	{
		if (!MarketCapHelpers.IsEligible(contract))
			return new FinancialsDto { Status = FinancialsStatus.NotEligible };

		var ticker = MarketCapHelpers.GetSecTicker(contract.Symbol);
		Task<FinancialsDto> lookup;
		lock (_lock)
		{
			var now = timeProvider.GetUtcNow();
			var cached = _cache.TryGetValue(ticker, out var entry) ? entry.Financials : null;
			if (cached != null && now - entry.At < GetTtl(cached))
				return cached;
			if (now < _retryAfter)
				return cached ?? new FinancialsDto { Status = FinancialsStatus.Unavailable };
			if (!_inFlight.TryGetValue(ticker, out lookup!))
			{
				// Not tied to one caller: others may be waiting on the same lookup
				lookup = Task.Run(() => LookUpAsync(ticker, cached));
				_inFlight[ticker] = lookup;
			}
		}
		return await lookup.WaitAsync(cancellationToken);
	}

	private async Task<FinancialsDto> LookUpAsync(string ticker, FinancialsDto? cached)
	{
		try
		{
			var ciks = await GetCiksAsync();
			var facts = ciks.TryGetValue(ticker, out var cik)
				? await secApiClient.GetCompanyFinancialFactsAsync(
					cik,
					FinancialsConstants.Concepts
				)
				: null;
			var financials =
				facts == null
					? new FinancialsDto { Status = FinancialsStatus.NotFiled }
					: FinancialsHelpers.Build(facts.Facts, facts.EntityName);
			lock (_lock)
				_cache[ticker] = (financials, timeProvider.GetUtcNow());
			return financials;
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
					$"Financial statements unavailable ({ticker}): {ex.Message}"
				);
			return cached ?? new FinancialsDto { Status = FinancialsStatus.Unavailable };
		}
		finally
		{
			lock (_lock)
				_inFlight.Remove(ticker);
		}
	}

	private static TimeSpan GetTtl(FinancialsDto financials) =>
		financials.Status == FinancialsStatus.Available
			? FinancialsConstants.FoundTtl
			: FinancialsConstants.NotFiledTtl;

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
