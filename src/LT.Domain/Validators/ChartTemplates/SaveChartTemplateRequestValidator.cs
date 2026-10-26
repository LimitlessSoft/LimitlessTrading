using FluentValidation;
using LSCore.Validation.Domain;
using LT.Contracts.Helpers;
using LT.Contracts.Requests.ChartTemplates;

namespace LT.Domain.Validators.ChartTemplates;

public class SaveChartTemplateRequestValidator : LSCoreValidatorBase<SaveChartTemplateRequest>
{
	public SaveChartTemplateRequestValidator()
	{
		RuleFor(x => x)
			.Custom(
				(request, context) =>
				{
					if (ChartTemplateHelpers.GetNameError(request.Name) is { } nameError)
						context.AddFailure(nameof(request.Name), nameError);
					else if (
						ChartTemplateHelpers.GetSettingsError(
							request.Name.Trim(),
							request.Settings
						) is
						{ } settingsError
					)
						context.AddFailure(nameof(request.Settings), settingsError);
				}
			);
	}
}
