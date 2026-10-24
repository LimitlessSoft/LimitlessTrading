using LT.Contracts.Requests.Connection;
using LT.Domain.Validators.Connection;

namespace LT.Tests.Domain.Validators;

public class ConnectRequestValidatorTests
{
	private readonly ConnectRequestValidator _validator = new();

	[Fact]
	public void Valid_request_passes() =>
		Assert.True(
			_validator
				.Validate(
					new ConnectRequest
					{
						Host = "127.0.0.1",
						Port = 7497,
						ClientId = 1,
					}
				)
				.IsValid
		);

	[Theory]
	[InlineData("", 7497, 1)]
	[InlineData("127.0.0.1", 0, 1)]
	[InlineData("127.0.0.1", 70000, 1)]
	[InlineData("127.0.0.1", 7497, -1)]
	public void Invalid_request_fails(string host, int port, int clientId) =>
		Assert.False(
			_validator
				.Validate(
					new ConnectRequest
					{
						Host = host,
						Port = port,
						ClientId = clientId,
					}
				)
				.IsValid
		);
}
