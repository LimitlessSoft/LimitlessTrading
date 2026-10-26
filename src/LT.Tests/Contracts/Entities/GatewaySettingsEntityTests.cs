using System.Text.Json;
using LT.Contracts.Entities;
using LT.Contracts.Enums;

namespace LT.Tests.Contracts.Entities;

public class GatewaySettingsEntityTests
{
	[Fact]
	public void Settings_saved_by_the_ibc_version_still_load()
	{
		const string json = """
			{
			  "Gateway": {
			    "Username": "trader1",
			    "TradingMode": 0,
			    "IbcPath": "/home/u/.config/LimitlessTrading/ibc",
			    "GatewayPath": "/home/u/Jts",
			    "GatewayVersion": "1050",
			    "UseVirtualDisplay": false
			  }
			}
			""";

		var gateway = JsonSerializer.Deserialize<SettingsEntity>(json)!.Gateway;

		Assert.Equal(
			(TradingMode.Live, "/home/u/Jts", "1050"),
			(gateway.TradingMode, gateway.GatewayPath, gateway.GatewayVersion)
		);
	}
}
