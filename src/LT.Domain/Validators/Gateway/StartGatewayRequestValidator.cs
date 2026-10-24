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
		RuleFor(x => x.Username)
			.NotEmpty()
			.WithMessage(GatewayValidationCodes.GVC_001.GetValidationMessage())
			.MaximumLength(GatewayConstants.UsernameMaxLength)
			.WithMessage(
				GatewayValidationCodes.GVC_002.GetValidationMessage(
					GatewayConstants.UsernameMaxLength
				)
			);

		// The password ends up as one line of IBC's config.ini
		RuleFor(x => x.Password)
			.NotEmpty()
			.WithMessage(GatewayValidationCodes.GVC_003.GetValidationMessage())
			.MaximumLength(GatewayConstants.PasswordMaxLength)
			.WithMessage(
				GatewayValidationCodes.GVC_004.GetValidationMessage(
					GatewayConstants.PasswordMaxLength
				)
			)
			.Must(x => !x.Contains('\n') && !x.Contains('\r'))
			.WithMessage(GatewayValidationCodes.GVC_005.GetValidationMessage());

		RuleFor(x => x.IbcPath)
			.NotEmpty()
			.WithMessage(GatewayValidationCodes.GVC_006.GetValidationMessage())
			.MaximumLength(GatewayConstants.PathMaxLength)
			.WithMessage(
				GatewayValidationCodes.GVC_008.GetValidationMessage(GatewayConstants.PathMaxLength)
			);

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

		RuleFor(x => x.TradingMode)
			.IsInEnum()
			.WithMessage(GatewayValidationCodes.GVC_010.GetValidationMessage());
	}
}
