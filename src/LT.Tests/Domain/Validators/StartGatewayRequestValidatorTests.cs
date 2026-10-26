using LT.Contracts.Requests.Gateway;
using LT.Domain.Validators.Gateway;

namespace LT.Tests.Domain.Validators;

public class StartGatewayRequestValidatorTests
{
	private readonly StartGatewayRequestValidator _validator = new();

	private static StartGatewayRequest Request(
		string gatewayPath = "/home/u/Jts",
		string version = "1037"
	) => new() { GatewayPath = gatewayPath, GatewayVersion = version };

	[Fact]
	public void Valid_request_passes() => Assert.True(_validator.Validate(Request()).IsValid);

	[Fact]
	public void Empty_gateway_path_fails() =>
		Assert.False(_validator.Validate(Request(gatewayPath: "")).IsValid);

	[Fact]
	public void Too_long_gateway_path_fails() =>
		Assert.False(_validator.Validate(Request(gatewayPath: new string('a', 1025))).IsValid);

	[Theory]
	[InlineData("")]
	[InlineData("10.37")]
	[InlineData("latest")]
	public void Bad_version_fails(string version) =>
		Assert.False(_validator.Validate(Request(version: version)).IsValid);
}
