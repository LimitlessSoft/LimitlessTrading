using System.Globalization;
using LT.App.Widgets.OptionChain;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.MarketData;
using LT.Contracts.Dtos.Options;

namespace LT.Tests.Widgets;

/// <summary>
/// The option chain's ask cells: hovering one shows what a click would buy and its estimated
/// cost, quantity × ask × multiplier, following the quote and the one-click quantity. No order is
/// ever sent (the row's trade callback fails the test).
/// </summary>
public class OptionRowViewModelBuyTipTests : IDisposable
{
	private readonly CultureInfo _culture = CultureInfo.CurrentCulture;
	private readonly OptionRowViewModel _row;

	public OptionRowViewModelBuyTipTests()
	{
		CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
		_row = new OptionRowViewModel(
			new OptionStrikeDto
			{
				Strike = 150,
				Call = Option("C", "100"),
				Put = Option("P", ""),
			},
			(_, _, _) => throw new InvalidOperationException("No order may be sent"),
			() => true
		);
	}

	[Fact]
	public void Says_there_is_no_price_until_the_ask_arrives() =>
		Assert.Equal("Buy 1 call at market — sends on click\nNo price", _row.CallBuyTip);

	[Fact]
	public void Shows_quantity_times_ask_times_multiplier()
	{
		_row.SetQuickOrderQuantity(2);
		_row.CallPending.Set(new QuoteDto { Bid = 1.3, Ask = 1.35 });
		_row.Flush();

		Assert.Equal(
			"Buy 2 calls at market — sends on click\n≈ 2 × 1.35 × 100 = 270.00 USD at the ask",
			_row.CallBuyTip
		);
	}

	[Fact]
	public void Follows_the_ask_and_the_quantity()
	{
		_row.CallPending.Set(new QuoteDto { Ask = 1.35 });
		_row.Flush();
		_row.CallPending.Set(new QuoteDto { Ask = 2.1 });
		_row.Flush();
		_row.SetQuickOrderQuantity(3);

		Assert.EndsWith("≈ 3 × 2.10 × 100 = 630.00 USD at the ask", _row.CallBuyTip);
	}

	[Fact]
	public void An_invalid_quantity_shows_why_instead_of_a_cost()
	{
		_row.CallPending.Set(new QuoteDto { Ask = 1.35 });
		_row.Flush();
		_row.SetQuickOrderQuantity(null);

		Assert.Equal(
			"Buy calls at market — sends on click\nSet a one-click quantity of 1 to 100 whole contracts.",
			_row.CallBuyTip
		);
	}

	[Fact]
	public void An_unknown_multiplier_shows_no_total()
	{
		_row.PutPending.Set(new QuoteDto { Ask = 0.5 });
		_row.Flush();

		Assert.Equal(
			"Buy 1 put at market — sends on click\n1 × 0.50 × ? — multiplier unknown",
			_row.PutBuyTip
		);
	}

	public void Dispose()
	{
		CultureInfo.CurrentCulture = _culture;
		GC.SuppressFinalize(this);
	}

	private static ContractDto Option(string right, string multiplier) =>
		new()
		{
			ConId = right == "C" ? 11 : 12,
			Symbol = "SPY",
			SecurityType = "OPT",
			Exchange = "SMART",
			Currency = "USD",
			LastTradeDateOrContractMonth = "20261120",
			Strike = 150,
			Right = right,
			Multiplier = multiplier,
		};
}
