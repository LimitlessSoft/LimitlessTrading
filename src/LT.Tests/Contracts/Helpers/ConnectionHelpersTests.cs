using LT.Contracts.Helpers;

namespace LT.Tests.Contracts.Helpers;

public class ConnectionHelpersTests
{
	[Theory]
	[InlineData(7496, "TWS live")]
	[InlineData(7497, "TWS paper")]
	[InlineData(4001, "Gateway live")]
	[InlineData(4002, "Gateway paper")]
	[InlineData(5555, "custom port")]
	public void DescribePort_names_well_known_ports(int port, string expected) =>
		Assert.Equal(expected, ConnectionHelpers.DescribePort(port));

	[Theory]
	[InlineData(null, new string[0])]
	[InlineData("", new string[0])]
	[InlineData("DU1", new[] { "DU1" })]
	[InlineData("DU1, DU2,", new[] { "DU1", "DU2" })]
	public void ParseAccounts_splits_managed_accounts(string? value, string[] expected) =>
		Assert.Equal(expected, ConnectionHelpers.ParseAccounts(value));
}
