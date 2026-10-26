using FluentValidation;
using LSCore.Validation.Domain;
using LT.Contracts.Helpers;
using LT.Contracts.Requests.ChartTemplates;

namespace LT.Domain.Validators.ChartTemplates;

public class RenameChartTemplateRequestValidator : LSCoreValidatorBase<RenameChartTemplateRequest>
{
	public RenameChartTemplateRequestValidator()
	{
		RuleFor(x => x)
			.Custom(
				(request, context) =>
				{
					if (ChartTemplateHelpers.GetNameError(request.Name) is { } error)
						context.AddFailure(nameof(request.Name), error);
				}
			);
	}
}
