using FluentValidation;
using LSCore.Validation.Contracts;
using LSCore.Validation.Domain;
using LT.Contracts.Constants;
using LT.Contracts.Enums.ValidationCodes;
using LT.Contracts.Requests.Connection;

namespace LT.Domain.Validators.Connection;

public class ConnectRequestValidator : LSCoreValidatorBase<ConnectRequest>
{
	public ConnectRequestValidator()
	{
		RuleFor(x => x.Host)
			.NotEmpty()
			.WithMessage(ConnectionValidationCodes.CVC_001.GetValidationMessage())
			.MaximumLength(ConnectionConstants.HostMaxLength)
			.WithMessage(
				ConnectionValidationCodes.CVC_002.GetValidationMessage(
					ConnectionConstants.HostMaxLength
				)
			);

		RuleFor(x => x.Port)
			.InclusiveBetween(ConnectionConstants.PortMin, ConnectionConstants.PortMax)
			.WithMessage(
				ConnectionValidationCodes.CVC_003.GetValidationMessage(
					ConnectionConstants.PortMin,
					ConnectionConstants.PortMax
				)
			);

		RuleFor(x => x.ClientId)
			.InclusiveBetween(ConnectionConstants.ClientIdMin, ConnectionConstants.ClientIdMax)
			.WithMessage(
				ConnectionValidationCodes.CVC_004.GetValidationMessage(
					ConnectionConstants.ClientIdMin,
					ConnectionConstants.ClientIdMax
				)
			);
	}
}
