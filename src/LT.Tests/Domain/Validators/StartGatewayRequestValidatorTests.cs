using LT.Contracts.Enums;
using LT.Contracts.Requests.Gateway;
using LT.Domain.Validators.Gateway;

namespace LT.Tests.Domain.Validators;

public class StartGatewayRequestValidatorTests
{
	private readonly StartGatewayRequestValidator _validator = new();

	private static StartGatewayRequest Request(
		string username = "trader1",
		string password = "s3cret",
		string ibcPath = "/opt/ibc",
		string gatewayPath = "/home/u/Jts",
		string version = "1037",
		TradingMode mode = TradingMode.Paper
	) =>
		new()
		{
			Username = username,
			Password = password,
			IbcPath = ibcPath,
			GatewayPath = gatewayPath,
			GatewayVersion = version,
			TradingMode = mode,
		};

	[Fact]
	public void Valid_request_passes() => Assert.True(_validator.Validate(Request()).IsValid);

	[Fact]
	public void Empty_username_fails() =>
		Assert.False(_validator.Validate(Request(username: "")).IsValid);

	[Theory]
	[InlineData("")]
	[InlineData("pass\nIbLoginId=other")]
	[InlineData("pass\r")]
	public void Bad_password_fails(string password) =>
		Assert.False(_validator.Validate(Request(password: password)).IsValid);

	[Fact]
	public void Empty_ibc_path_fails() =>
		Assert.False(_validator.Validate(Request(ibcPath: "")).IsValid);

	[Fact]
	public void Empty_gateway_path_fails() =>
		Assert.False(_validator.Validate(Request(gatewayPath: "")).IsValid);

	[Theory]
	[InlineData("")]
	[InlineData("10.37")]
	[InlineData("latest")]
	public void Bad_version_fails(string version) =>
		Assert.False(_validator.Validate(Request(version: version)).IsValid);

	[Fact]
	public void Unknown_mode_fails() =>
		Assert.False(_validator.Validate(Request(mode: (TradingMode)42)).IsValid);
}
