using LSCore.Exceptions;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.Orders;
using LT.Contracts.Enums;
using LT.Contracts.Exceptions;
using LT.Contracts.Requests.Orders;
using LT.Domain.Managers;
using LT.Tests.Fakes;

namespace LT.Tests.Domain.Managers;

/// <summary>
/// A new limit price for a working order goes to IB only for this app's own limit orders, and the
/// shared open orders hear that this app's orders changed. Nothing here reaches IB: the fake
/// records what would be sent.
/// </summary>
public class OrderManagerModifyTests
{
	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	private readonly FakeIbkrApiClient _ibkr = new() { IsConnected = true, OrderClientId = 101 };
	private readonly TestCache _cache = new();
	private readonly OrderManager _manager;
	private int _changes;

	public OrderManagerModifyTests()
	{
		LSCoreContainer.EnsureInitialized();
		_manager = new OrderManager(_ibkr, _cache.Manager);
		_manager.OrdersChanged += () => Interlocked.Increment(ref _changes);
	}

	private static OpenOrderDto Order(int clientId = 101, string type = "LMT") =>
		new()
		{
			OrderId = 44,
			ClientId = clientId,
			PermId = 900000044,
			Account = "U0000001",
			Symbol = "SPY 20261029 767 P",
			SecurityType = "OPT",
			Action = "SELL",
			Quantity = 1,
			OrderType = type,
			LimitPrice = 1.60,
			TimeInForce = "DAY",
			Status = "Submitted",
			Contract = new ContractDto { ConId = 800000001, Exchange = "SMART" },
		};

	[Fact]
	public async Task A_new_price_for_this_apps_limit_order_is_sent_with_the_order_as_read()
	{
		var order = Order();

		await _manager.ModifyOrderAsync(
			new ModifyOrderRequest { Order = order, LimitPrice = 1.55m },
			Ct
		);

		var (sent, price) = Assert.Single(_ibkr.ModifiedOrders);
		Assert.Same(order, sent);
		Assert.Equal(1.55m, price);
		Assert.Empty(_ibkr.PlacedOrders);
		Assert.Equal(1, _changes);
	}

	[Theory]
	[InlineData(0)] // TWS
	[InlineData(7)] // another API client
	public async Task Another_clients_order_is_refused_and_nothing_is_sent(int clientId)
	{
		var error = await Assert.ThrowsAsync<LSCoreBadRequestException>(() =>
			_manager.ModifyOrderAsync(
				new ModifyOrderRequest { Order = Order(clientId), LimitPrice = 1.55m },
				Ct
			)
		);

		Assert.Contains("TWS", error.Message);
		Assert.Empty(_ibkr.ModifiedOrders);
	}

	[Fact]
	public async Task Disconnected_nothing_is_sent()
	{
		_ibkr.OrderClientId = null;

		await Assert.ThrowsAsync<LSCoreBadRequestException>(() =>
			_manager.ModifyOrderAsync(
				new ModifyOrderRequest { Order = Order(), LimitPrice = 1.55m },
				Ct
			)
		);

		Assert.Empty(_ibkr.ModifiedOrders);
	}

	[Fact]
	public async Task A_stop_order_is_not_changed()
	{
		await Assert.ThrowsAsync<LSCoreBadRequestException>(() =>
			_manager.ModifyOrderAsync(
				new ModifyOrderRequest { Order = Order(type: "STP LMT"), LimitPrice = 1.55m },
				Ct
			)
		);

		Assert.Empty(_ibkr.ModifiedOrders);
	}

	[Theory]
	[InlineData(null)]
	[InlineData(0.0)]
	[InlineData(-1.0)]
	[InlineData(1.60)] // unchanged
	public async Task An_invalid_price_is_not_sent(double? price)
	{
		await Assert.ThrowsAsync<LSCoreBadRequestException>(() =>
			_manager.ModifyOrderAsync(
				new ModifyOrderRequest { Order = Order(), LimitPrice = (decimal?)price },
				Ct
			)
		);

		Assert.Empty(_ibkr.ModifiedOrders);
	}

	[Fact]
	public async Task Without_an_order_nothing_is_sent()
	{
		await Assert.ThrowsAsync<LSCoreBadRequestException>(() =>
			_manager.ModifyOrderAsync(new ModifyOrderRequest { LimitPrice = 1.55m }, Ct)
		);

		Assert.Empty(_ibkr.ModifiedOrders);
	}

	[Fact]
	public async Task Ibs_refusal_reaches_the_caller_and_the_orders_are_read_again()
	{
		_ibkr.ModifyException = new IbkrRequestException(
			110,
			"The price does not conform to the minimum price variation for this contract."
		);

		var error = await Assert.ThrowsAsync<IbkrRequestException>(() =>
			_manager.ModifyOrderAsync(
				new ModifyOrderRequest { Order = Order(), LimitPrice = 1.555m },
				Ct
			)
		);

		Assert.Equal(110, error.Code);
		Assert.Equal(1, _changes);
	}

	[Fact]
	public void The_reason_an_order_cant_be_changed_is_for_the_user()
	{
		Assert.Null(_manager.GetModifyBlockReason(Order()));
		Assert.Contains("TWS", _manager.GetModifyBlockReason(Order(0)));
	}

	[Fact]
	public async Task A_cancel_and_a_placed_orders_statuses_say_the_orders_changed()
	{
		_ibkr.OrderStatuses =
		[
			new OrderStatusDto { OrderId = 100, Status = "Submitted" },
			new OrderStatusDto
			{
				OrderId = 100,
				Status = "Cancelled",
				IsDone = true,
			},
		];

		await _manager.CancelOrderAsync(44, Ct);
		Assert.Equal(1, _changes);

		var placed = await _manager.PlaceOrderAsync(
			new PlaceOrderRequest
			{
				Contract = new ContractDto { ConId = 756733, SecurityType = "STK" },
				Account = "U0000001",
				Action = OrderAction.Buy,
				OrderType = OrderType.Limit,
				Quantity = 1,
				LimitPrice = 500,
				TimeInForce = TimeInForce.Day,
			},
			Ct
		);
		await foreach (var _ in placed.Statuses.WithCancellation(Ct)) { }

		Assert.Equal(3, _changes);
	}
}
