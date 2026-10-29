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

		// No contract is the broad feeds; a contract needs a conId (IB) or at least a symbol (the
		// app's feeds and the archive, while disconnected)
		RuleFor(x => x.Contract)
			.Must(x => x!.ConId > 0 || !string.IsNullOrWhiteSpace(x.Symbol))
			.When(x => x.Contract != null)
			.WithMessage(NewsValidationCodes.NVC_006.GetValidationMessage());
	}
}
