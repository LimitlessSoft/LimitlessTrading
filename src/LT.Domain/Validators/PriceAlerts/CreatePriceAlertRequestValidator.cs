using FluentValidation;
using LSCore.Validation.Contracts;
using LSCore.Validation.Domain;
using LT.Contracts.Constants;
using LT.Contracts.Enums.ValidationCodes;
using LT.Contracts.Requests.PriceAlerts;

namespace LT.Domain.Validators.PriceAlerts;

public class CreatePriceAlertRequestValidator : LSCoreValidatorBase<CreatePriceAlertRequest>
{
	public CreatePriceAlertRequestValidator()
	{
		RuleFor(x => x.Contract)
			.Must(x => x is { ConId: > 0 })
			.WithMessage(PriceAlertValidationCodes.PAVC_001.GetValidationMessage());

		RuleFor(x => x.Price)
			.Must(x => double.IsFinite(x) && x > 0)
			.WithMessage(PriceAlertValidationCodes.PAVC_002.GetValidationMessage());

		RuleFor(x => x.CurrentPrice)
			.Must(x => x is { } price && double.IsFinite(price) && price > 0)
			.WithMessage(PriceAlertValidationCodes.PAVC_004.GetValidationMessage());

		RuleFor(x => x.Note)
			.Must(x => x == null || x.Trim().Length <= PriceAlertConstants.MaxNoteLength)
			.WithMessage(
				PriceAlertValidationCodes.PAVC_003.GetValidationMessage(
					PriceAlertConstants.MaxNoteLength
				)
			);
	}
}
