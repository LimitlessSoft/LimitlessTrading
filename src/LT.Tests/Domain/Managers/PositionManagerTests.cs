using LT.Contracts.Dtos.Positions;
using LT.Domain.Managers;
using LT.Tests.Fakes;

namespace LT.Tests.Domain.Managers;

public class PositionManagerTests
{
	private readonly FakeIbkrApiClient _ibkr = new();

	[Fact]
	public async Task Returns_nothing_when_disconnected()
	{
		_ibkr.Positions = [new PositionDto { Symbol = "AAPL", Quantity = 1 }];

		var positions = await new PositionManager(_ibkr).GetPositionsAsync(
			TestContext.Current.CancellationToken
		);

		Assert.Empty(positions);
	}

	[Fact]
	public async Task Drops_closed_positions_and_sorts_by_account_then_symbol()
	{
		_ibkr.IsConnected = true;
		_ibkr.Positions =
		[
			new PositionDto
			{
				Account = "B",
				Symbol = "AAPL",
				Quantity = 1,
			},
			new PositionDto
			{
				Account = "A",
				Symbol = "MSFT",
				Quantity = 2,
			},
			new PositionDto
			{
				Account = "A",
				Symbol = "AMD",
				Quantity = 3,
			},
			new PositionDto
			{
				Account = "A",
				Symbol = "TSLA",
				Quantity = 0,
			},
		];

		var positions = await new PositionManager(_ibkr).GetPositionsAsync(
			TestContext.Current.CancellationToken
		);

		Assert.Equal(["AMD", "MSFT", "AAPL"], positions.Select(x => x.Symbol));
	}
}
