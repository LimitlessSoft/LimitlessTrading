using LT.Contracts.Dtos.Fundamentals;
using LT.Contracts.Interfaces.IApiClients;

namespace LT.Tests.Fakes;

/// <summary>Canned SEC answers by CIK; counts every call. <see cref="Gate"/> holds lookups back.</summary>
public class FakeSecApiClient : ISecApiClient
{
	public Dictionary<string, int> Ciks { get; } = new(StringComparer.OrdinalIgnoreCase);
	public Dictionary<int, List<SecFactDto>> ConceptFacts { get; } = [];
	public Dictionary<int, List<SecFactDto>> CompanyFacts { get; } = [];
	public Exception? Failure { get; set; }

	/// <summary>When set, concept lookups wait for it (to overlap concurrent lookups).</summary>
	public TaskCompletionSource? Gate { get; set; }

	public int TickerCalls { get; private set; }
	public int ConceptCalls { get; private set; }
	public int CompanyFactsCalls { get; private set; }

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

	public static SecFactDto Fact(
		string end,
		long value,
		string form = "10-Q",
		string? filed = null
	) =>
		new()
		{
			End = DateOnly.Parse(end),
			Filed = DateOnly.Parse(filed ?? end),
			Value = value,
			Form = form,
		};
}
