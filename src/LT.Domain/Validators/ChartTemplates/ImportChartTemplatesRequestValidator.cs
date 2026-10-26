using FluentValidation;
using LSCore.Validation.Contracts;
using LSCore.Validation.Domain;
using LT.Contracts.Constants;
using LT.Contracts.Enums.ValidationCodes;
using LT.Contracts.Helpers;
using LT.Contracts.Requests.ChartTemplates;

namespace LT.Domain.Validators.ChartTemplates;

/// <summary>
/// The file must be ours and hold at least one template; every template a valid name and
/// settings. The first problem is reported (naming its template), so the user can fix the file.
/// </summary>
public class ImportChartTemplatesRequestValidator : LSCoreValidatorBase<ImportChartTemplatesRequest>
{
	public ImportChartTemplatesRequestValidator()
	{
		RuleFor(x => x)
			.Custom(
				(request, context) =>
				{
					var error =
						request.Format != ChartTemplateConstants.FileFormat
							? ChartTemplateValidationCodes.CHTVC_005.GetValidationMessage()
						: request.Templates.Count == 0
							? ChartTemplateValidationCodes.CHTVC_006.GetValidationMessage()
						: request.Templates.Count > ChartTemplateConstants.MaxTemplatesPerFile
							? ChartTemplateValidationCodes.CHTVC_007.GetValidationMessage(
								ChartTemplateConstants.MaxTemplatesPerFile
							)
						: request
							.Templates.Select(x =>
								ChartTemplateHelpers.GetNameError(x.Name)
								?? ChartTemplateHelpers.GetSettingsError(x.Name.Trim(), x.Settings)
							)
							.FirstOrDefault(x => x != null);
					if (error != null)
						context.AddFailure(nameof(request.Templates), error);
				}
			);
	}
}
