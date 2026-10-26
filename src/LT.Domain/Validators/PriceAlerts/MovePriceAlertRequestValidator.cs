using FluentValidation;
using LSCore.Validation.Contracts;
using LSCore.Validation.Domain;
using LT.Contracts.Enums.ValidationCodes;
using LT.Contracts.Requests.PriceAlerts;

namespace LT.Domain.Validators.PriceAlerts;

public class MovePriceAlertRequestValidator : LSCoreValidatorBase<MovePriceAlertRequest>
{
	public MovePriceAlertRequestValidator()
	{
		RuleFor(x => x.Price)
			.Must(x => double.IsFinite(x) && x > 0)
			.WithMessage(PriceAlertValidationCodes.PAVC_002.GetValidationMessage());

		RuleFor(x => x.CurrentPrice)
			.Must(x => x is { } price && double.IsFinite(price) && price > 0)
			.WithMessage(PriceAlertValidationCodes.PAVC_004.GetValidationMessage());
	}
}
