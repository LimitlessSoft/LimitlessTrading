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

		// No contract is the broad tape; a contract needs a conId (IB) or at least a symbol (the
		// app's feeds, while disconnected)
		RuleFor(x => x.Contract)
			.Must(x => x!.ConId > 0 || !string.IsNullOrWhiteSpace(x.Symbol))
			.When(x => x.Contract != null)
			.WithMessage(NewsValidationCodes.NVC_006.GetValidationMessage());
	}
}
