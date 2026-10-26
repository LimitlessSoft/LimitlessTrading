using LT.Contracts.Dtos.Charts;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.Positions;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;

namespace LT.Tests.Contracts.Helpers;

public class ChartTradeHelpersTests
{
	// 09:30 in New York
	private static readonly DateTimeOffset Open = new(2026, 10, 26, 13, 30, 0, TimeSpan.Zero);
	private static readonly TimeZoneInfo NewYork = TradeJournalHelpers.GetTradeDayTimeZone();

	[Theory]
	[InlineData(0, 0)] // on the first bar's start
	[InlineData(4, 0)]
	[InlineData(5, 1)]
	[InlineData(14, 2)] // the last bar, within its length
	[InlineData(15, -1)] // after the last bar
	[InlineData(-1, -1)] // before the first bar
	public void An_intraday_fill_snaps_to_the_bar_that_contains_it(int minute, int expected)
	{
		var bars = Bars(Open, TimeSpan.FromMinutes(5), 3);

		Assert.Equal(
			expected,
			ChartTradeHelpers.FindBarIndex(
				bars,
				Open.AddMinutes(minute),
				ChartTimeframe.FiveMinutes,
				NewYork
			)
		);
	}

	[Fact]
	public void A_fill_in_a_gap_between_sessions_has_no_bar()
	{
		// 15:55 bar, then the next morning's 09:30 bar
		var close = Open.AddHours(6).AddMinutes(25);
		var bars = new List<BarDto>
		{
			new() { Time = close },
			new() { Time = Open.AddDays(1) },
		};

		Assert.Equal(
			-1,
			ChartTradeHelpers.FindBarIndex(
				bars,
				close.AddMinutes(30),
				ChartTimeframe.FiveMinutes,
				NewYork
			)
		);
	}

	[Fact]
	public void A_daily_fill_lands_on_its_new_york_date()
	{
		// Daily bars carry the date at midnight UTC
		var bars = Bars(
			new DateTimeOffset(2026, 10, 26, 0, 0, 0, TimeSpan.Zero),
			TimeSpan.FromDays(1),
			3
		);
		// 21:00 in New York on the 26th is already the 27th in UTC
		var evening = new DateTimeOffset(2026, 10, 27, 1, 0, 0, TimeSpan.Zero);

		Assert.Equal(
			0,
			ChartTradeHelpers.FindBarIndex(bars, evening, ChartTimeframe.OneDay, NewYork)
		);
	}

	[Fact]
	public void A_weekly_bar_contains_the_whole_week()
	{
		var bars = Bars(
			new DateTimeOffset(2026, 10, 19, 0, 0, 0, TimeSpan.Zero),
			TimeSpan.FromDays(7),
			2
		);

		Assert.Equal(
			1,
			ChartTradeHelpers.FindBarIndex(bars, Open, ChartTimeframe.OneWeek, NewYork)
		);
	}

	[Fact]
	public void No_bars_means_no_bar() =>
		Assert.Equal(
			-1,
			ChartTradeHelpers.FindBarIndex([], Open, ChartTimeframe.OneMinute, NewYork)
		);

	[Theory]
	[InlineData("", 10, 10.5, 10.5)] // stock: per share
	[InlineData("100", 1, 250, 2.5)] // option: per contract
	[InlineData("100", -2, 250, 2.5)] // short
	[InlineData("100", 0, 250, null)] // flat
	public void Break_even_is_the_average_cost_per_unit(
		string multiplier,
		int quantity,
		double averageCost,
		double? expected
	) =>
		Assert.Equal(
			expected,
			ChartTradeHelpers.GetBreakEvenPrice(
				new PositionDto
				{
					Quantity = quantity,
					AverageCost = averageCost,
					Contract = new ContractDto { Multiplier = multiplier },
				}
			)
		);

	private static List<BarDto> Bars(DateTimeOffset start, TimeSpan length, int count) =>
		Enumerable
			.Range(0, count)
			.Select(i => new BarDto { Time = start + length * i, Close = 10 })
			.ToList();
}
