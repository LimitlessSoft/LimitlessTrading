using FluentValidation;
using LSCore.Validation.Contracts;
using LSCore.Validation.Domain;
using LT.Contracts.Constants;
using LT.Contracts.Enums.ValidationCodes;
using LT.Contracts.Requests.ChartDrawings;

namespace LT.Domain.Validators.ChartDrawings;

/// <summary>The points' count against the drawing's kind is checked by the manager, which knows the kind.</summary>
public class UpdateChartDrawingRequestValidator : LSCoreValidatorBase<UpdateChartDrawingRequest>
{
	public UpdateChartDrawingRequestValidator()
	{
		RuleFor(x => x.Id)
			.NotEmpty()
			.WithMessage(ChartDrawingValidationCodes.CDVC_009.GetValidationMessage());

		RuleFor(x => x.Points)
			.Must(x =>
				x is { Count: > 0 and <= ChartDrawingConstants.MaxBrushPoints }
				&& x.All(p => p != null && double.IsFinite(p.Price))
			)
			.WithMessage(ChartDrawingValidationCodes.CDVC_004.GetValidationMessage());

		RuleFor(x => x.Text)
			.Must(x => x == null || x.Trim().Length <= ChartDrawingConstants.MaxTextLength)
			.WithMessage(
				ChartDrawingValidationCodes.CDVC_006.GetValidationMessage(
					ChartDrawingConstants.MaxTextLength
				)
			);
	}
}
