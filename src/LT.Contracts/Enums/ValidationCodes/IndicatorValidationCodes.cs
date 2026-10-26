using LSCore.Validation.Contracts;

namespace LT.Contracts.Enums.ValidationCodes;

public enum IndicatorValidationCodes
{
	[LSCoreValidationMessage("The indicator {0} is no longer available.")]
	IVC_001,

	[LSCoreValidationMessage("{0} is not an input of this indicator.")]
	IVC_002,

	[LSCoreValidationMessage("{0} must be a number.")]
	IVC_003,

	[LSCoreValidationMessage("{0} must be a whole number.")]
	IVC_004,

	[LSCoreValidationMessage("{0} must be at least {1}.")]
	IVC_005,

	[LSCoreValidationMessage("{0} must be at most {1}.")]
	IVC_006,

	[LSCoreValidationMessage("{0} must be on or off.")]
	IVC_007,

	[LSCoreValidationMessage("A chart can show at most {0} indicators.")]
	IVC_008,

	[LSCoreValidationMessage("Bars are required.")]
	IVC_009,
}
