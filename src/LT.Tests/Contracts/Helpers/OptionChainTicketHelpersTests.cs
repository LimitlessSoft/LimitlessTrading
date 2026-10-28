using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.MarketData;
using LT.Contracts.Dtos.Options;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;

namespace LT.Tests.Contracts.Helpers;

/// <summary>
/// The option chain's strike click: which option the ticket opens for, what it is pre-filled
/// with, and the quote columns the chain can hide.
/// </summary>
public class OptionChainTicketHelpersTests
{
	private static ContractDto Option(int conId, string right) =>
		new()
		{
			ConId = conId,
			Symbol = "SPY",
			SecurityType = "OPT",
			LastTradeDateOrContractMonth = "20261120",
			Strike = 150,
			Right = right,
		};

	private static OptionStrikeDto Strike(bool withPut = true) =>
		new()
		{
			Strike = 150,
			Call = Option(11, "C"),
			Put = withPut ? Option(12, "P") : null,
		};

	[Theory]
	[InlineData("C", 11)]
	[InlineData("P", 12)]
	public void The_draft_is_for_the_clicked_side(string right, int conId)
	{
		var draft = OptionChainHelpers.GetOrderDraft(Strike(), right, "U1", 3, null);

		Assert.NotNull(draft);
		Assert.Equal(conId, draft.Contract.ConId);
		Assert.Equal(OrderAction.Buy, draft.Action);
		Assert.Equal(3m, draft.Quantity);
		Assert.Equal("U1", draft.Account);
		Assert.Null(draft.LimitPrice);
		Assert.StartsWith($"SPY 20261120 150 {right} from the option chain", draft.Note);
	}

	[Fact]
	public void No_draft_for_a_side_without_an_option_or_an_unknown_right()
	{
		Assert.Null(OptionChainHelpers.GetOrderDraft(Strike(withPut: false), "P", "U1", 1, null));
		Assert.Null(OptionChainHelpers.GetOrderDraft(Strike(), "X", "U1", 1, null));
	}

	[Theory]
	[InlineData(null)]
	[InlineData(0d)]
	[InlineData(1.5)]
	[InlineData(500d)]
	public void An_invalid_one_click_quantity_drafts_one_contract(double? quantity)
	{
		var draft = OptionChainHelpers.GetOrderDraft(Strike(), "C", "U1", (decimal?)quantity, null);

		Assert.Equal(1m, draft!.Quantity);
	}

	[Fact]
	public void The_limit_box_starts_at_the_mid()
	{
		var draft = OptionChainHelpers.GetOrderDraft(
			Strike(),
			"C",
			"U1",
			1,
			new QuoteDto { Bid = 1.20, Ask = 1.25 }
		);

		Assert.Equal(1.23m, draft!.LimitPrice);
	}

	[Theory]
	[InlineData(1.0, 1.1, null, 1.05)]
	[InlineData(null, 1.1, 0.9, 0.9)]
	[InlineData(0.0, 1.1, null, null)]
	[InlineData(1.2, 1.1, 0.95, 0.95)]
	[InlineData(null, null, null, null)]
	public void Mid_price_falls_back_to_the_last_then_none(
		double? bid,
		double? ask,
		double? last,
		double? expected
	) =>
		Assert.Equal(
			(decimal?)expected,
			OptionChainHelpers.GetMidPrice(
				new QuoteDto
				{
					Bid = bid,
					Ask = ask,
					Last = last,
				}
			)
		);

	[Fact]
	public void Hidden_columns_drop_unknown_values_and_save_in_menu_order()
	{
		var hidden = OptionChainHelpers.ResolveHiddenColumns([
			OptionQuoteColumn.Bid,
			(OptionQuoteColumn)42,
			OptionQuoteColumn.Volume,
		]);

		Assert.Equal(
			[OptionQuoteColumn.Volume, OptionQuoteColumn.Bid],
			OptionChainHelpers.ToSavedHiddenColumns(hidden)
		);
		Assert.Empty(OptionChainHelpers.ResolveHiddenColumns(null));
		Assert.Null(OptionChainHelpers.ToSavedHiddenColumns(new HashSet<OptionQuoteColumn>()));
	}
}
