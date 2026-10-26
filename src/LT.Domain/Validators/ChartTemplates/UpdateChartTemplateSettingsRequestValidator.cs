using FluentValidation;
using LSCore.Validation.Contracts;
using LSCore.Validation.Domain;
using LT.Contracts.Constants;
using LT.Contracts.Enums.ValidationCodes;
using LT.Contracts.Helpers;
using LT.Contracts.Requests.ChartTemplates;

namespace LT.Domain.Validators.ChartTemplates;

public class UpdateChartTemplateSettingsRequestValidator
	: LSCoreValidatorBase<UpdateChartTemplateSettingsRequest>
{
	public UpdateChartTemplateSettingsRequestValidator()
	{
		RuleFor(x => x)
			.Custom(
				(request, context) =>
				{
					if (string.IsNullOrWhiteSpace(request.Id))
						context.AddFailure(
							nameof(request.Id),
							ChartTemplateValidationCodes.CHTVC_004.GetValidationMessage()
						);
					else if (
						ChartTemplateHelpers.GetSettingsError(
							ChartTemplateConstants.UnnamedTemplateLabel,
							request.Settings
						) is
						{ } settingsError
					)
						context.AddFailure(nameof(request.Settings), settingsError);
				}
			);
	}
}
