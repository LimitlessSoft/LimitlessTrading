using LT.Contracts.Dtos.Contracts;
using LT.Ibkr.DtoMappings.Contracts;

namespace LT.Tests.Ibkr;

public class IbkrContractMapperTests
{
	[Fact]
	public void A_missing_strike_is_sent_as_any_strike_not_zero() =>
		Assert.Null(
			new IbkrContractMapper()
				.ToMapped(
					new ContractDto
					{
						Symbol = "SPY",
						SecurityType = "OPT",
						LastTradeDateOrContractMonth = "20261026",
					}
				)
				.Strike
		);

	[Fact]
	public void A_resolved_contract_is_sent_by_conid_on_smart() =>
		Assert.Equal(
			new IBKR.Net.Contract { ConId = 756733, Exchange = "SMART" },
			new IbkrContractMapper().ToMapped(new ContractDto { ConId = 756733, Symbol = "SPY" })
		);
}
