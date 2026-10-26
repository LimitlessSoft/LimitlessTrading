using FluentValidation;
using LSCore.Validation.Contracts;
using LSCore.Validation.Domain;
using LT.Contracts.Constants;
using LT.Contracts.Enums.ValidationCodes;
using LT.Contracts.Requests.Gateway;

namespace LT.Domain.Validators.Gateway;

public class StartGatewayRequestValidator : LSCoreValidatorBase<StartGatewayRequest>
{
	public StartGatewayRequestValidator()
	{
		RuleFor(x => x.GatewayPath)
			.NotEmpty()
			.WithMessage(GatewayValidationCodes.GVC_007.GetValidationMessage())
			.MaximumLength(GatewayConstants.PathMaxLength)
			.WithMessage(
				GatewayValidationCodes.GVC_008.GetValidationMessage(GatewayConstants.PathMaxLength)
			);

		RuleFor(x => x.GatewayVersion)
			.Matches(GatewayConstants.VersionPattern)
			.WithMessage(GatewayValidationCodes.GVC_009.GetValidationMessage());
	}
}
