using LT.Contracts.Dtos.Contracts;
using LT.Ibkr.DtoMappings.Contracts;

namespace LT.Tests.Ibkr;

public class IbContractMapperTests
{
	[Fact]
	public void A_missing_strike_is_sent_as_unset_not_zero()
	{
		var contract = new IbContractMapper().ToMapped(
			new ContractDto
			{
				Symbol = "SPY",
				SecurityType = "OPT",
				LastTradeDateOrContractMonth = "20261026",
			}
		);

		Assert.Equal(double.MaxValue, contract.Strike);
	}

	[Fact]
	public void A_strike_is_kept() =>
		Assert.Equal(
			580,
			new IbContractMapper()
				.ToMapped(
					new ContractDto
					{
						Symbol = "SPY",
						SecurityType = "OPT",
						Strike = 580,
					}
				)
				.Strike
		);
}
