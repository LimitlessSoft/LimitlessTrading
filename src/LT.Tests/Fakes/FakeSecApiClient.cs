using LT.Contracts.Constants;
using LT.Contracts.Dtos.Calendar;
using LT.Contracts.Dtos.Fundamentals;
using LT.Contracts.Interfaces.IApiClients;

namespace LT.Tests.Fakes;

/// <summary>Canned SEC answers by CIK; counts every call. <see cref="Gate"/> holds lookups back.</summary>
public class FakeSecApiClient : ISecApiClient
{
	public Dictionary<string, int> Ciks { get; } = new(StringComparer.OrdinalIgnoreCase);
	public Dictionary<int, List<SecFactDto>> ConceptFacts { get; } = [];
	public Dictionary<int, List<SecFactDto>> CompanyFacts { get; } = [];
	public Dictionary<int, List<SecFactDto>> FilingFacts { get; } = [];
	public Dictionary<int, SecCompanyFactsDto> FinancialFacts { get; } = [];
	public Exception? Failure { get; set; }

	/// <summary>When set, concept lookups wait for it (to overlap concurrent lookups).</summary>
	public TaskCompletionSource? Gate { get; set; }

	public int TickerCalls { get; private set; }
	public int ConceptCalls { get; private set; }
	public int CompanyFactsCalls { get; private set; }
	public int FilingCalls { get; private set; }
	public int FinancialCalls { get; private set; }

	/// <summary>The latest filings feed, newest first; each call answers the page at its start.</summary>
	public List<SecCurrentFilingDto> CurrentFilings { get; } = [];

	/// <summary>The start of every feed page asked for.</summary>
	public List<int> CurrentFilingsStarts { get; } = [];

	public Task<IReadOnlyList<SecCurrentFilingDto>> GetCurrentFilingsAsync(
		string form,
		int start,
		CancellationToken cancellationToken = default
	)
	{
		CurrentFilingsStarts.Add(start);
		if (Failure != null)
			return Task.FromException<IReadOnlyList<SecCurrentFilingDto>>(Failure);
		return Task.FromResult<IReadOnlyList<SecCurrentFilingDto>>([
			.. CurrentFilings.Skip(start).Take(SecConstants.CurrentFilingsPageSize),
		]);
	}

	public Task<IReadOnlyDictionary<string, int>> GetTickerCiksAsync(
		CancellationToken cancellationToken = default
	)
	{
		TickerCalls++;
		return Failure != null
			? Task.FromException<IReadOnlyDictionary<string, int>>(Failure)
			: Task.FromResult<IReadOnlyDictionary<string, int>>(
				new Dictionary<string, int>(Ciks, StringComparer.OrdinalIgnoreCase)
			);
	}

	public async Task<IReadOnlyList<SecFactDto>?> GetSharesOutstandingFactsAsync(
		int cik,
		CancellationToken cancellationToken = default
	)
	{
		ConceptCalls++;
		if (Gate != null)
			await Gate.Task;
		if (Failure != null)
			throw Failure;
		return ConceptFacts.GetValueOrDefault(cik);
	}

	public Task<IReadOnlyList<SecFactDto>?> GetCompanyShareFactsAsync(
		int cik,
		CancellationToken cancellationToken = default
	)
	{
		CompanyFactsCalls++;
		return Task.FromResult<IReadOnlyList<SecFactDto>?>(CompanyFacts.GetValueOrDefault(cik));
	}

	public Task<IReadOnlyList<SecFactDto>?> GetLatestFilingShareFactsAsync(
		int cik,
		CancellationToken cancellationToken = default
	)
	{
		FilingCalls++;
		return Task.FromResult<IReadOnlyList<SecFactDto>?>(FilingFacts.GetValueOrDefault(cik));
	}

	public async Task<SecCompanyFactsDto?> GetCompanyFinancialFactsAsync(
		int cik,
		IReadOnlyCollection<string> concepts,
		CancellationToken cancellationToken = default
	)
	{
		FinancialCalls++;
		if (Gate != null)
			await Gate.Task;
		if (Failure != null)
			throw Failure;
		return FinancialFacts.GetValueOrDefault(cik);
	}

	/// <summary>A count as of <paramref name="end"/>; with <paramref name="start"/> a weighted average.</summary>
	public static SecFactDto Fact(
		string end,
		long value,
		string form = "10-Q",
		string? filed = null,
		string? start = null,
		string concept = SecConstants.SharesOutstandingConcept,
		string? shareClass = null
	) =>
		new()
		{
			Concept = concept,
			Start = start == null ? null : DateOnly.Parse(start),
			ShareClass = shareClass,
			End = DateOnly.Parse(end),
			Filed = DateOnly.Parse(filed ?? end),
			Value = value,
			Form = form,
		};
}
