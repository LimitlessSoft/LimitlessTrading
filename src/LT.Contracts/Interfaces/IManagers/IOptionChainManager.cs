using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.Options;
using LT.Contracts.Requests.Options;

namespace LT.Contracts.Interfaces.IManagers;

public interface IOptionChainManager
{
	/// <summary>Every expiration of the underlying's options, nearest first.</summary>
	Task<IReadOnlyList<OptionExpirationDto>> GetExpirationsAsync(
		ContractDto underlying,
		CancellationToken cancellationToken = default
	);

	Task<OptionChainDto> GetChainAsync(
		GetOptionChainRequest request,
		CancellationToken cancellationToken = default
	);
}
