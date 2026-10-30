using FluentValidation;
using LSCore.Validation.Contracts;
using LSCore.Validation.Domain;
using LT.Contracts.Constants;
using LT.Contracts.Enums.ValidationCodes;
using LT.Contracts.Helpers;
using LT.Contracts.Requests.GammaExposure;

namespace LT.Domain.Validators.GammaExposure;

public class GetGammaExposureRequestValidator : LSCoreValidatorBase<GetGammaExposureRequest>
{
	public GetGammaExposureRequestValidator()
	{
		RuleFor(x => x.Expiration)
			.Matches(GammaExposureHelpers.ExpirationRegex())
			.WithMessage(GammaExposureValidationCodes.GXVC_001.GetValidationMessage());

		RuleFor(x => x.StrikesAroundSpot)
			.InclusiveBetween(1, GammaExposureConstants.MaxStrikesAroundSpot)
			.WithMessage(
				GammaExposureValidationCodes.GXVC_002.GetValidationMessage(
					GammaExposureConstants.MaxStrikesAroundSpot
				)
			);

		RuleFor(x => x.Underlying)
			.Must(ContractHelpers.SupportsOptionChain)
			.WithMessage(OptionChainValidationCodes.OCVC_002.GetValidationMessage());

		RuleFor(x => x.Underlying.ConId)
			.GreaterThan(0)
			.WithMessage(ContractValidationCodes.CTVC_004.GetValidationMessage());
	}
}
