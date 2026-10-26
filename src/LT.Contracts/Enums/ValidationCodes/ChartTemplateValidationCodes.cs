using LSCore.Validation.Contracts;

namespace LT.Contracts.Enums.ValidationCodes;

public enum ChartTemplateValidationCodes
{
	[LSCoreValidationMessage("Template name is required.")]
	CHTVC_001,

	[LSCoreValidationMessage("Template name can be at most {0} characters long.")]
	CHTVC_002,

	[LSCoreValidationMessage("A template named {0} already exists.")]
	CHTVC_003,

	[LSCoreValidationMessage("That template no longer exists.")]
	CHTVC_004,

	[LSCoreValidationMessage("This file is not a chart template file.")]
	CHTVC_005,

	[LSCoreValidationMessage("The file has no templates.")]
	CHTVC_006,

	[LSCoreValidationMessage("A file can hold at most {0} templates.")]
	CHTVC_007,

	[LSCoreValidationMessage("{0}: unknown timeframe.")]
	CHTVC_008,

	[LSCoreValidationMessage("{0}: a template can hold at most {1} indicators.")]
	CHTVC_009,

	[LSCoreValidationMessage("{0}: an indicator has no type.")]
	CHTVC_010,

	[LSCoreValidationMessage("{0}: indicator input {1} must be a number.")]
	CHTVC_011,

	[LSCoreValidationMessage("{0}: the right margin must be a number of at least 0.")]
	CHTVC_012,

	[LSCoreValidationMessage("The file is larger than {0} MB.")]
	CHTVC_013,
}
