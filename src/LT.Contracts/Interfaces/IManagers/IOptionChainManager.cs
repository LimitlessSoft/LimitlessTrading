using LT.Contracts.Dtos.Options;
using LT.Contracts.Requests.Options;

namespace LT.Contracts.Interfaces.IManagers;

public interface IOptionChainManager
{
	Task<OptionChainDto> GetChainAsync(
		GetOptionChainRequest request,
		CancellationToken cancellationToken = default
	);
}
