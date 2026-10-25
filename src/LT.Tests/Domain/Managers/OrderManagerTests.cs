using LSCore.Exceptions;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.Orders;
using LT.Contracts.Enums;
using LT.Contracts.Requests.Orders;
using LT.Domain.Managers;
using LT.Tests.Fakes;

namespace LT.Tests.Domain.Managers;

public class OrderManagerTests
{
	private readonly FakeIbkrApiClient _ibkr = new() { IsConnected = true };
	private readonly OrderManager _manager;

	public OrderManagerTests()
	{
		LSCoreContainer.EnsureInitialized();
		_manager = new OrderManager(_ibkr);
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
}
