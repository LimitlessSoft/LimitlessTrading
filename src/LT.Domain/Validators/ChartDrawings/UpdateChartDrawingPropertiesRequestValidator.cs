using FluentValidation;
using LSCore.Validation.Contracts;
using LSCore.Validation.Domain;
using LT.Contracts.Enums.ValidationCodes;
using LT.Contracts.Requests.ChartDrawings;

namespace LT.Domain.Validators.ChartDrawings;

public class UpdateChartDrawingPropertiesRequestValidator
	: LSCoreValidatorBase<UpdateChartDrawingPropertiesRequest>
{
	public UpdateChartDrawingPropertiesRequestValidator()
	{
		RuleFor(x => x.Id)
			.NotEmpty()
			.WithMessage(ChartDrawingValidationCodes.CDVC_009.GetValidationMessage());

		this.AddStyleRules(x => x.Style);
		this.AddFibLevelRules(x => x.FibLevels);
	}
}
