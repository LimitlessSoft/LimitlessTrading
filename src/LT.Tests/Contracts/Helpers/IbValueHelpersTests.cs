using LT.Contracts.Helpers;

namespace LT.Tests.Contracts.Helpers;

public class IbValueHelpersTests
{
	[Theory]
	[InlineData(double.MaxValue)]
	[InlineData(double.NaN)]
	[InlineData(double.PositiveInfinity)]
	public void Unset_values_become_null(double value) => Assert.Null(IbValueHelpers.Clean(value));

	[Fact]
	public void Real_values_pass_through() => Assert.Equal(-12.5, IbValueHelpers.Clean(-12.5));
}
