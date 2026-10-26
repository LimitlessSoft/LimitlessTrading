using FluentValidation;
using LSCore.Validation.Contracts;
using LSCore.Validation.Domain;
using LT.Contracts.Enums.ValidationCodes;
using LT.Contracts.Requests.News;

namespace LT.Domain.Validators.News;

public class SubscribeNewsRequestValidator : LSCoreValidatorBase<SubscribeNewsRequest>
{
	public SubscribeNewsRequestValidator()
	{
		RuleFor(x => x.Providers)
			.NotEmpty()
			.WithMessage(NewsValidationCodes.NVC_001.GetValidationMessage());

		// No contract is the broad tape; a contract must be resolved
		RuleFor(x => x.Contract!.ConId)
			.GreaterThan(0)
			.When(x => x.Contract != null)
			.WithMessage(ContractValidationCodes.CTVC_004.GetValidationMessage());
	}
}
