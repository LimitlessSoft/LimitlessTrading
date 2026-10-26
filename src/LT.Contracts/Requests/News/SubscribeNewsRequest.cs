using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.News;

namespace LT.Contracts.Requests.News;

/// <summary>Live headlines of a contract, or with no contract the providers' broad tapes.</summary>
public class SubscribeNewsRequest
{
	/// <summary>Null: every headline of the providers (broad tape).</summary>
	public ContractDto? Contract { get; set; }

	/// <summary>Of any source; each source is asked for its own.</summary>
	public IReadOnlyList<NewsProviderDto> Providers { get; set; } = [];
}
