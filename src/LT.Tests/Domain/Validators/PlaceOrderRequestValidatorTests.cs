using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Enums;
using LT.Contracts.Requests.Orders;
using LT.Domain.Validators.Orders;

namespace LT.Tests.Domain.Validators;

public class PlaceOrderRequestValidatorTests
{
	private readonly PlaceOrderRequestValidator _validator = new();

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

	private bool IsValid(PlaceOrderRequest request) => _validator.Validate(request).IsValid;

	[Fact]
	public void Ten_dollars_of_btc_at_market_passes() => Assert.True(IsValid(TenDollarsOfBtc()));

	[Fact]
	public void Crypto_limit_gtc_with_a_quantity_passes()
	{
		var request = TenDollarsOfBtc();
		request.OrderType = OrderType.Limit;
		request.CashAmount = null;
		request.Quantity = 0.0002m;
		request.LimitPrice = 20000;
		request.TimeInForce = TimeInForce.Gtc;

		Assert.True(IsValid(request));
	}

	[Fact]
	public void A_contract_and_an_account_are_required()
	{
		var request = TenDollarsOfBtc();
		request.Contract = new ContractDto { Symbol = "BTC" };
		Assert.False(IsValid(request));

		request = TenDollarsOfBtc();
		request.Account = " ";
		Assert.False(IsValid(request));
	}

	[Fact]
	public void Exactly_one_of_quantity_and_amount()
	{
		var request = TenDollarsOfBtc();
		request.CashAmount = null;
		Assert.False(IsValid(request));

		request = TenDollarsOfBtc();
		request.Quantity = 1;
		Assert.False(IsValid(request));
	}

	[Theory]
	[InlineData(0.5)]
	[InlineData(10_001)]
	public void Amount_out_of_bounds_fails(double amount)
	{
		var request = TenDollarsOfBtc();
		request.CashAmount = (decimal)amount;

		Assert.False(IsValid(request));
	}

	[Fact]
	public void An_amount_needs_a_market_order()
	{
		var request = TenDollarsOfBtc();
		request.OrderType = OrderType.Limit;
		request.LimitPrice = 20000;
		request.TimeInForce = TimeInForce.Gtc;

		Assert.False(IsValid(request));
	}

	[Fact]
	public void A_limit_order_needs_a_positive_price()
	{
		var request = TenDollarsOfBtc();
		request.CashAmount = null;
		request.Quantity = 1;
		request.OrderType = OrderType.Limit;
		request.TimeInForce = TimeInForce.Gtc;
		request.LimitPrice = 0;

		Assert.False(IsValid(request));
	}

	[Fact]
	public void Crypto_rejects_day_orders()
	{
		var request = TenDollarsOfBtc();
		request.TimeInForce = TimeInForce.Day;

		var result = _validator.Validate(request);

		Assert.False(result.IsValid);
		Assert.Contains(result.Errors, x => x.ErrorMessage.Contains("IOC"));
	}

	[Fact]
	public void Zero_quantity_fails()
	{
		var request = TenDollarsOfBtc();
		request.CashAmount = null;
		request.Quantity = 0;

		Assert.False(IsValid(request));
	}
}
