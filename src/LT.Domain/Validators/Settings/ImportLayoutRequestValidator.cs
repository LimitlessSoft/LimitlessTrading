using FluentValidation;
using LSCore.Validation.Contracts;
using LSCore.Validation.Domain;
using LT.Contracts.Constants;
using LT.Contracts.Enums.ValidationCodes;
using LT.Contracts.Helpers;
using LT.Contracts.Requests.Settings;

namespace LT.Domain.Validators.Settings;

/// <summary>
/// The file must be within the size limit, JSON of the layout file's shape, marked as ours, of a
/// version this app reads, and hold a layout. The first problem is reported.
/// </summary>
public class ImportLayoutRequestValidator : LSCoreValidatorBase<ImportLayoutRequest>
{
	public ImportLayoutRequestValidator()
	{
		RuleFor(x => x)
			.Custom(
				(request, context) =>
				{
					var error = GetError(request.Json ?? string.Empty);
					if (error != null)
						context.AddFailure(nameof(request.Json), error);
				}
			);
	}

	private static string? GetError(string json)
	{
		if (json.Length > LayoutFileConstants.MaxFileLength)
			return SettingsValidationCodes.SVC_009.GetValidationMessage(
				LayoutFileConstants.MaxFileLength / (1024 * 1024)
			);
		if (string.IsNullOrWhiteSpace(json))
			return SettingsValidationCodes.SVC_011.GetValidationMessage();
		var file = LayoutFileHelpers.ParseFile(json);
		if (file == null)
			return SettingsValidationCodes.SVC_011.GetValidationMessage();
		if (file.Format != LayoutFileConstants.FileFormat)
			return SettingsValidationCodes.SVC_008.GetValidationMessage();
		if (file.Version > LayoutFileConstants.CurrentVersion)
			return SettingsValidationCodes.SVC_010.GetValidationMessage(
				file.Version,
				LayoutFileConstants.CurrentVersion
			);
		if (file.Version < 1 || file.Layout == null)
			return SettingsValidationCodes.SVC_011.GetValidationMessage();
		return null;
	}
}
