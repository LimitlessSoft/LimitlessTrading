using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Interfaces.IManagers;
using LT.Contracts.Requests.Contracts;

namespace LT.Tests.Fakes;

/// <summary>
/// Symbol lookups (and finds by a contract's fields) from tables: a known symbol gives its
/// listings, any other fails like IB does for an unknown symbol. Records the lookups and how many
/// ran at once.
/// </summary>
public class FakeContractManager : IContractManager
{
	private int _running;

	public Dictionary<string, ContractDto> Symbols { get; } = new(StringComparer.OrdinalIgnoreCase);
	public List<string> Lookups { get; } = [];
	public int MaxConcurrent { get; private set; }

	public Task<IReadOnlyList<ContractDetailsDto>> SearchAsync(
		ContractSearchRequest request,
		CancellationToken cancellationToken = default
	) => Task.FromResult<IReadOnlyList<ContractDetailsDto>>([]);

	public async Task<IReadOnlyList<ContractDetailsDto>> SearchSymbolAsync(
		string symbol,
		CancellationToken cancellationToken = default
	)
	{
		MaxConcurrent = Math.Max(MaxConcurrent, Interlocked.Increment(ref _running));
		try
		{
			Lookups.Add(symbol);
			await Task.Yield();
			cancellationToken.ThrowIfCancellationRequested();
			return Symbols.TryGetValue(symbol, out var contract)
				? [new ContractDetailsDto { Contract = contract }]
				: throw new InvalidOperationException($"No security definition for {symbol}");
		}
		finally
		{
			Interlocked.Decrement(ref _running);
		}
	}

	/// <summary>
	/// What <see cref="FindAsync"/> answers for a contract known by its fields, by symbol; any
	/// other symbol fails like IB does. Each find is recorded in <see cref="Lookups"/> too.
	/// </summary>
	public Dictionary<string, List<ContractDto>> Found { get; } =
		new(StringComparer.OrdinalIgnoreCase);

	/// <summary>The contracts <see cref="FindAsync"/> was asked for, in order.</summary>
	public List<ContractDto> Finds { get; } = [];

	public async Task<IReadOnlyList<ContractDetailsDto>> FindAsync(
		ContractDto contract,
		CancellationToken cancellationToken = default
	)
	{
		MaxConcurrent = Math.Max(MaxConcurrent, Interlocked.Increment(ref _running));
		try
		{
			Lookups.Add(contract.Symbol);
			Finds.Add(contract);
			await Task.Yield();
			cancellationToken.ThrowIfCancellationRequested();
			return Found.TryGetValue(contract.Symbol, out var contracts)
				? [.. contracts.Select(x => new ContractDetailsDto { Contract = x })]
				: throw new InvalidOperationException(
					$"No security definition for {contract.Symbol}"
				);
		}
		finally
		{
			Interlocked.Decrement(ref _running);
		}
	}

	/// <summary>What <see cref="GetDetailsAsync"/> answers, by conId; any other gives null.</summary>
	public Dictionary<int, ContractDetailsDto> Details { get; } = [];

	public Task<ContractDetailsDto?> GetDetailsAsync(
		ContractDto contract,
		CancellationToken cancellationToken = default
	) => Task.FromResult(Details.GetValueOrDefault(contract.ConId));

	public Task<ContractDto> ResolveTickerAsync(
		string ticker,
		CancellationToken cancellationToken = default
	) => throw new NotSupportedException();

	public IReadOnlyList<ContractDto> GetRecentContracts() => [];

	public void AddRecentContract(ContractDto contract) { }
}
