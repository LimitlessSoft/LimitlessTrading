using LT.Contracts.Dtos.Flex;
using LT.Contracts.Interfaces.IApiClients;
using LT.Contracts.Requests.Flex;

namespace LT.Tests.Fakes;

/// <summary>Answers each statement request with <see cref="Handler"/> and records the requests.</summary>
public class FakeFlexApiClient : IFlexApiClient
{
	public List<FlexStatementRequest> Requests { get; } = [];

	public Func<FlexStatementRequest, FlexStatementDto> Handler { get; set; } =
		_ => new FlexStatementDto { HasNavSection = true, HasTradesSection = true };

	public Task<FlexStatementDto> GetStatementAsync(
		FlexStatementRequest request,
		CancellationToken cancellationToken = default
	)
	{
		Requests.Add(request);
		return Task.FromResult(Handler(request));
	}
}
