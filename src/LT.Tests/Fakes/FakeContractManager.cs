using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Interfaces.IManagers;
using LT.Contracts.Requests.Contracts;

namespace LT.Tests.Fakes;

/// <summary>
/// Symbol lookups from a table: a known symbol gives its listing, any other fails like IB does
/// for an unknown symbol. Records the lookups and how many ran at once.
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

	public Task<ContractDto> ResolveTickerAsync(
		string ticker,
		CancellationToken cancellationToken = default
	) => throw new NotSupportedException();

	public IReadOnlyList<ContractDto> GetRecentContracts() => [];

	public void AddRecentContract(ContractDto contract) { }
}
