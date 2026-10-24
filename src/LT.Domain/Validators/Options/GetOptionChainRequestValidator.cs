using FluentValidation;
using LSCore.Validation.Contracts;
using LSCore.Validation.Domain;
using LT.Contracts.Enums.ValidationCodes;
using LT.Contracts.Helpers;
using LT.Contracts.Requests.Options;

namespace LT.Domain.Validators.Options;

public class GetOptionChainRequestValidator : LSCoreValidatorBase<GetOptionChainRequest>
{
	public GetOptionChainRequestValidator()
	{
		RuleFor(x => x.Expiry)
			.Matches(OptionChainHelpers.ExpiryRegex())
			.WithMessage(OptionChainValidationCodes.OCVC_001.GetValidationMessage());

		RuleFor(x => x.Underlying)
			.Must(ContractHelpers.SupportsOptionChain)
			.WithMessage(OptionChainValidationCodes.OCVC_002.GetValidationMessage());

		RuleFor(x => x.Underlying.ConId)
			.GreaterThan(0)
			.WithMessage(ContractValidationCodes.CTVC_004.GetValidationMessage());
	}
}
