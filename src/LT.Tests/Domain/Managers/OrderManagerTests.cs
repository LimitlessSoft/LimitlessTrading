using System.Threading.Channels;
using LSCore.Exceptions;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.Orders;
using LT.Contracts.Dtos.Positions;
using LT.Contracts.Enums;
using LT.Contracts.Requests.Orders;
using LT.Domain.Managers;
using LT.Tests.Fakes;

namespace LT.Tests.Domain.Managers;

public class OrderManagerTests
{
	private readonly FakeIbkrApiClient _ibkr = new() { IsConnected = true };
	private readonly TestCache _cache = new();
	private readonly OrderManager _manager;

	public OrderManagerTests()
	{
		LSCoreContainer.EnsureInitialized();
		_manager = new OrderManager(_ibkr, _cache.Manager);
	}

	private static PlaceOrderRequest TenDollarsOfBtc() =>
		new()
		{
			Contract = new ContractDto
			{
				ConId = 479624278,
				Symbol = "BTC",
				SecurityType = "CRYPTO",
				Exchange = "PAXOS",
			},
			Account = "U0000001",
			Action = OrderAction.Buy,
			OrderType = OrderType.Market,
			CashAmount = 10,
			TimeInForce = TimeInForce.Ioc,
		};

	[Fact]
	public async Task A_valid_order_is_sent_with_its_cash_amount()
	{
		_ibkr.OrderStatuses =
		[
			new OrderStatusDto
			{
				OrderId = 100,
				Status = "Filled",
				IsDone = true,
			},
		];

		var placed = await _manager.PlaceOrderAsync(
			TenDollarsOfBtc(),
			TestContext.Current.CancellationToken
		);

		var (contract, order) = Assert.Single(_ibkr.PlacedOrders);
		Assert.Equal(479624278, contract.ConId);
		Assert.Equal(
			(
				OrderAction.Buy,
				OrderType.Market,
				0m,
				10m,
				(decimal?)null,
				TimeInForce.Ioc,
				"U0000001"
			),
			(
				order.Action,
				order.OrderType,
				order.Quantity,
				order.CashQuantity!.Value,
				order.LimitPrice,
				order.TimeInForce,
				order.Account
			)
		);
		Assert.Equal(100, placed.OrderId);
		Assert.Equal(
			["Filled"],
			await placed
				.Statuses.Select(x => x.Status)
				.ToListAsync(TestContext.Current.CancellationToken)
		);
	}

	[Fact]
	public async Task An_invalid_order_is_not_sent()
	{
		var request = TenDollarsOfBtc();
		request.Account = "";

		await Assert.ThrowsAsync<LSCoreBadRequestException>(() =>
			_manager.PlaceOrderAsync(request, TestContext.Current.CancellationToken)
		);
		Assert.Empty(_ibkr.PlacedOrders);
	}

	[Fact]
	public async Task Cancel_goes_to_ib()
	{
		await _manager.CancelOrderAsync(100, TestContext.Current.CancellationToken);

		Assert.Equal([100], _ibkr.CancelledOrderIds);
	}

	private static PositionDto Position(decimal quantity, int conId = 265598) =>
		new()
		{
			Account = "U0000001",
			ContractId = conId,
			Symbol = "AAPL",
			SecurityType = "STK",
			Quantity = quantity,
			Contract = new ContractDto
			{
				ConId = conId,
				Symbol = "AAPL",
				SecurityType = "STK",
				Exchange = "SMART",
			},
		};

	[Theory]
	[InlineData(100, OrderAction.Sell, 100)]
	[InlineData(-40, OrderAction.Buy, 40)]
	public async Task Exit_sends_the_whole_position_at_market(
		int quantity,
		OrderAction action,
		int expected
	)
	{
		var placed = await _manager.ExitPositionAsync(
			Position(quantity),
			TestContext.Current.CancellationToken
		);

		var (contract, order) = Assert.Single(_ibkr.PlacedOrders);
		Assert.Equal(265598, contract.ConId);
		Assert.Equal(
			(action, OrderType.Market, (decimal)expected, (decimal?)null, "U0000001"),
			(order.Action, order.OrderType, order.Quantity, order.CashQuantity, order.Account)
		);
		Assert.Equal(100, placed.OrderId);
	}

	[Fact]
	public async Task Exit_of_a_flat_position_sends_nothing()
	{
		await Assert.ThrowsAsync<LSCoreBadRequestException>(() =>
			_manager.ExitPositionAsync(Position(0), TestContext.Current.CancellationToken)
		);
		Assert.Empty(_ibkr.PlacedOrders);
	}

	[Fact]
	public async Task A_second_exit_is_refused_while_the_first_is_working()
	{
		var working = Channel.CreateUnbounded<OrderStatusDto>();
		_ibkr.OrderStatusSource = () => working.Reader.ReadAllAsync();
		var cancellationToken = TestContext.Current.CancellationToken;

		var first = await _manager.ExitPositionAsync(Position(100), cancellationToken);
		var following = first.Statuses.ToListAsync(cancellationToken).AsTask();

		await Assert.ThrowsAsync<LSCoreBadRequestException>(() =>
			_manager.ExitPositionAsync(Position(100), cancellationToken)
		);
		Assert.Single(_ibkr.PlacedOrders);

		// Another position isn't held up by it
		await _manager.ExitPositionAsync(Position(10, conId: 76792991), cancellationToken);
		Assert.Equal(2, _ibkr.PlacedOrders.Count);

		working.Writer.Complete();
		await following;
	}

	[Fact]
	public async Task An_exit_can_be_sent_again_once_the_first_is_done()
	{
		_ibkr.OrderStatuses =
		[
			new OrderStatusDto
			{
				OrderId = 100,
				Status = "Cancelled",
				IsDone = true,
			},
		];
		var cancellationToken = TestContext.Current.CancellationToken;

		var first = await _manager.ExitPositionAsync(Position(100), cancellationToken);
		await first.Statuses.ToListAsync(cancellationToken);
		await _manager.ExitPositionAsync(Position(100), cancellationToken);

		Assert.Equal(2, _ibkr.PlacedOrders.Count);
	}
}
