using IBKR.Net;
using LT.Ibkr.DtoMappings.Executions;
using LT.Tests.Fakes;

namespace LT.Tests.Ibkr;

public class IbkrExecutionDtoMapperTests
{
	public IbkrExecutionDtoMapperTests() => LSCoreContainer.EnsureInitialized();

	private static Execution Fill(Contract contract, string side = "BOT") =>
		new(
			"0000e0d5.6576fd38.01.01",
			12,
			3,
			900000002,
			contract,
			"U0000001",
			"CBOE",
			side,
			2,
			1.25,
			2,
			1.25,
			""
		);

	private static readonly Contract Option = new()
	{
		ConId = 712345678,
		Symbol = "SPY",
		LocalSymbol = "SPY   261030C00580000",
		SecurityType = "OPT",
		Exchange = "CBOE",
		Currency = "USD",
		Strike = 580,
		Right = "C",
		Multiplier = "100",
	};

	[Fact]
	public void A_fill_maps_its_contract_side_and_commission_report()
	{
		var time = new DateTimeOffset(2026, 10, 26, 10, 0, 59, TimeSpan.FromHours(-4));
		var dto = new IbkrExecutionDtoMapper().ToMapped(
			Fill(Option, "SLD") with
			{
				Time = time,
				TimeText = "20261026 10:00:59 US/Eastern",
				Commission = new CommissionReport("0000e0d5.6576fd38.01.01", 1.3, "USD", 48.7),
			}
		);

		Assert.Equal("0000e0d5.6576fd38.01.01", dto.ExecId);
		Assert.Equal(time, dto.Time);
		Assert.Equal("U0000001", dto.Account);
		Assert.Equal(712345678, dto.ConId);
		Assert.Equal("SPY", dto.Symbol);
		Assert.Equal("SPY   261030C00580000", dto.LocalSymbol);
		Assert.Equal("OPT", dto.SecurityType);
		Assert.Equal(100, dto.Multiplier);
		Assert.Equal("SLD", dto.Side);
		Assert.Equal(2, dto.Shares);
		Assert.Equal(1.25, dto.Price);
		Assert.Equal("CBOE", dto.Exchange);
		Assert.Equal(12, dto.OrderId);
		Assert.Equal(1.3, dto.Commission);
		Assert.Equal("USD", dto.CommissionCurrency);
		Assert.Equal(48.7, dto.RealizedPnl);
	}

	[Fact]
	public void A_fill_without_a_known_zone_takes_its_time_from_the_text()
	{
		var dto = new IbkrExecutionDtoMapper().ToMapped(
			Fill(Option) with
			{
				TimeText = "20261026 10:00:59 UTC",
			}
		);

		Assert.Equal(new DateTimeOffset(2026, 10, 26, 10, 0, 59, TimeSpan.Zero), dto.Time);
	}

	[Theory]
	[InlineData("")]
	[InlineData("x")]
	[InlineData("0")]
	public void A_stock_fill_without_a_multiplier_or_commission_maps_them_to_null(string multiplier)
	{
		var dto = new IbkrExecutionDtoMapper().ToMapped(
			Fill(
				new Contract
				{
					ConId = 265598,
					Symbol = "AAPL",
					SecurityType = "STK",
					Multiplier = multiplier,
				}
			)
		);

		Assert.Null(dto.Multiplier);
		Assert.Null(dto.Commission);
		Assert.Equal(string.Empty, dto.CommissionCurrency);
		Assert.Null(dto.RealizedPnl);
		Assert.Null(dto.Time);
		Assert.Equal("BOT", dto.Side);
	}

	[Fact]
	public void A_fill_carries_its_contract_for_the_link_group_without_the_fill_venue()
	{
		var dto = new IbkrExecutionDtoMapper().ToMapped(Fill(Option));

		Assert.Equal(712345678, dto.Contract.ConId);
		Assert.Equal("SPY", dto.Contract.Symbol);
		Assert.Equal("SPY   261030C00580000", dto.Contract.LocalSymbol);
		Assert.Equal("OPT", dto.Contract.SecurityType);
		Assert.Equal("USD", dto.Contract.Currency);
		Assert.Equal(580, dto.Contract.Strike);
		Assert.Equal("C", dto.Contract.Right);
		Assert.Equal("100", dto.Contract.Multiplier);
		// Linked widgets ask for data through SMART, not the venue the fill happened on
		Assert.Equal(string.Empty, dto.Contract.Exchange);
		Assert.Equal("CBOE", dto.Exchange);
	}
}
