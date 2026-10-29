using FluentValidation;
using LSCore.Validation.Contracts;
using LSCore.Validation.Domain;
using LT.Contracts.Constants;
using LT.Contracts.Enums.ValidationCodes;
using LT.Contracts.Requests.News;

namespace LT.Domain.Validators.News;

public class SearchNewsRequestValidator : LSCoreValidatorBase<SearchNewsRequest>
{
	public SearchNewsRequestValidator()
	{
		RuleFor(x => x.Query)
			.Must(x => (x ?? string.Empty).Trim().Length >= NewsConstants.SearchMinLength)
			.WithMessage(
				NewsValidationCodes.NVC_007.GetValidationMessage(NewsConstants.SearchMinLength)
			);

		RuleFor(x => x.Limit)
			.InclusiveBetween(1, NewsConstants.MaxHeadlinesPerRequest)
			.WithMessage(
				NewsValidationCodes.NVC_002.GetValidationMessage(
					NewsConstants.MaxHeadlinesPerRequest
				)
			);
	}
}
