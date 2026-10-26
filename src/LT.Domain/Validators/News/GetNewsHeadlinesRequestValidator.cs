using FluentValidation;
using LSCore.Validation.Contracts;
using LSCore.Validation.Domain;
using LT.Contracts.Constants;
using LT.Contracts.Enums.ValidationCodes;
using LT.Contracts.Requests.News;

namespace LT.Domain.Validators.News;

public class GetNewsHeadlinesRequestValidator : LSCoreValidatorBase<GetNewsHeadlinesRequest>
{
	public GetNewsHeadlinesRequestValidator()
	{
		RuleFor(x => x.Providers)
			.NotEmpty()
			.WithMessage(NewsValidationCodes.NVC_001.GetValidationMessage());

		RuleFor(x => x.Limit)
			.InclusiveBetween(1, NewsConstants.MaxHeadlinesPerRequest)
			.WithMessage(
				NewsValidationCodes.NVC_002.GetValidationMessage(
					NewsConstants.MaxHeadlinesPerRequest
				)
			);

		RuleFor(x => x.Contract.ConId)
			.GreaterThan(0)
			.WithMessage(ContractValidationCodes.CTVC_004.GetValidationMessage());
	}
}
