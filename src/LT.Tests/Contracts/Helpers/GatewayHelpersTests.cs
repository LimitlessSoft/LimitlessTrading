using LT.Contracts.Enums;
using LT.Contracts.Helpers;

namespace LT.Tests.Contracts.Helpers;

public class GatewayHelpersTests
{
	[Theory]
	[InlineData(TradingMode.Live, 4001)]
	[InlineData(TradingMode.Paper, 4002)]
	public void GetApiPort_maps_mode_to_gateway_port(TradingMode mode, int expected) =>
		Assert.Equal(expected, GatewayHelpers.GetApiPort(mode));

	[Fact]
	public void GetApiPorts_lists_both_modes_preferred_first()
	{
		Assert.Equal(
			[(TradingMode.Live, 4001), (TradingMode.Paper, 4002)],
			GatewayHelpers.GetApiPorts(TradingMode.Live)
		);
		Assert.Equal(
			[(TradingMode.Paper, 4002), (TradingMode.Live, 4001)],
			GatewayHelpers.GetApiPorts(TradingMode.Paper)
		);
	}
}
