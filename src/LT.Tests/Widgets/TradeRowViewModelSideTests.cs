using LT.App.Widgets.DayTradePerformance;
using LT.Contracts.Dtos.Performance;
using LT.Contracts.Enums;

namespace LT.Tests.Widgets;

/// <summary>
/// The trades table's Side column: Long / Short for stocks and other instruments, a dash for
/// options (a bought put is "long" yet bearish, so the word would mislead).
/// </summary>
public class TradeRowViewModelSideTests
{
	private static TradeRowViewModel Row(string securityType, TradeSide side) =>
		new(
			new RoundTripDto
			{
				Symbol = "SPY",
				SecurityType = securityType,
				Side = side,
				Quantity = 1,
			},
			TimeZoneInfo.Utc
		);

	[Theory]
	[InlineData("STK", TradeSide.Long, "Long")]
	[InlineData("STK", TradeSide.Short, "Short")]
	[InlineData("FUT", TradeSide.Short, "Short")]
	[InlineData("", TradeSide.Long, "Long")]
	public void NonOption_ShowsDirection(string securityType, TradeSide side, string expected) =>
		Assert.Equal(expected, Row(securityType, side).Side);

	[Theory]
	[InlineData("OPT", TradeSide.Long)]
	[InlineData("OPT", TradeSide.Short)]
	[InlineData("opt", TradeSide.Long)]
	[InlineData("FOP", TradeSide.Long)]
	public void Option_ShowsDash(string securityType, TradeSide side) =>
		Assert.Equal("—", Row(securityType, side).Side);
}
