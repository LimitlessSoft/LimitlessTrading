using FluentValidation;
using LSCore.Validation.Contracts;
using LSCore.Validation.Domain;
using LT.Contracts.Constants;
using LT.Contracts.Enums.ValidationCodes;
using LT.Contracts.Requests.Indicators;

namespace LT.Domain.Validators.Indicators;

public class ComputeIndicatorsRequestValidator : LSCoreValidatorBase<ComputeIndicatorsRequest>
{
	public ComputeIndicatorsRequestValidator()
	{
		RuleFor(x => x.Bars)
			.NotNull()
			.WithMessage(IndicatorValidationCodes.IVC_009.GetValidationMessage());

		RuleFor(x => x.Indicators)
			.Must(x => x.Count <= IndicatorConstants.MaxIndicatorsPerChart)
			.WithMessage(
				IndicatorValidationCodes.IVC_008.GetValidationMessage(
					IndicatorConstants.MaxIndicatorsPerChart
				)
			);
	}
}
