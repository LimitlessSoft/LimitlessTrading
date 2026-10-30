using FluentValidation;
using LSCore.Validation.Contracts;
using LSCore.Validation.Domain;
using LT.Contracts.Constants;
using LT.Contracts.Enums.ValidationCodes;
using LT.Contracts.Requests.Settings;

namespace LT.Domain.Validators.Settings;

public class SaveUiScaleRequestValidator : LSCoreValidatorBase<SaveUiScaleRequest>
{
	public SaveUiScaleRequestValidator()
	{
		RuleFor(x => x.Percent)
			.InclusiveBetween(UiScaleConstants.MinPercent, UiScaleConstants.MaxPercent)
			.WithMessage(
				SettingsValidationCodes.SVC_015.GetValidationMessage(
					UiScaleConstants.MinPercent,
					UiScaleConstants.MaxPercent
				)
			);
	}
}
