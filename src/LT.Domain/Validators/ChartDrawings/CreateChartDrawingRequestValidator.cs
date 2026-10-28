using FluentValidation;
using LSCore.Validation.Contracts;
using LSCore.Validation.Domain;
using LT.Contracts.Constants;
using LT.Contracts.Enums;
using LT.Contracts.Enums.ValidationCodes;
using LT.Contracts.Helpers;
using LT.Contracts.Requests.ChartDrawings;

namespace LT.Domain.Validators.ChartDrawings;

public class CreateChartDrawingRequestValidator : LSCoreValidatorBase<CreateChartDrawingRequest>
{
	public CreateChartDrawingRequestValidator()
	{
		RuleFor(x => x.ConId)
			.GreaterThan(0)
			.WithMessage(ChartDrawingValidationCodes.CDVC_001.GetValidationMessage());

		RuleFor(x => x.Kind)
			.IsInEnum()
			.WithMessage(ChartDrawingValidationCodes.CDVC_002.GetValidationMessage());

		RuleFor(x => x)
			.Must(x => ChartDrawingHelpers.HasValidPointCount(x.Kind, x.Points?.Count ?? 0))
			.WithMessage(ChartDrawingValidationCodes.CDVC_003.GetValidationMessage());

		RuleFor(x => x.Points)
			.Must(x => x == null || x.All(p => p != null && double.IsFinite(p.Price)))
			.WithMessage(ChartDrawingValidationCodes.CDVC_004.GetValidationMessage());

		RuleFor(x => x.Text)
			.Must(x => !string.IsNullOrWhiteSpace(x))
			.When(x => x.Kind == ChartDrawingKind.Text)
			.WithMessage(ChartDrawingValidationCodes.CDVC_005.GetValidationMessage());

		RuleFor(x => x.Text)
			.Must(x => x == null || x.Trim().Length <= ChartDrawingConstants.MaxTextLength)
			.WithMessage(
				ChartDrawingValidationCodes.CDVC_006.GetValidationMessage(
					ChartDrawingConstants.MaxTextLength
				)
			);

		this.AddStyleRules(x => x.Style);
		this.AddFibLevelRules(x => x.FibLevels);
	}
}
