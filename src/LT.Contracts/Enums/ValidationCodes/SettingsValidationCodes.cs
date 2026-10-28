using LSCore.Validation.Contracts;

namespace LT.Contracts.Enums.ValidationCodes;

public enum SettingsValidationCodes
{
	[LSCoreValidationMessage("Heartbeat interval must be between {0} and {1} seconds.")]
	SVC_001,

	[LSCoreValidationMessage("Unknown market data type.")]
	SVC_002,

	[LSCoreValidationMessage("Unknown layout style.")]
	SVC_003,

	[LSCoreValidationMessage("Maximum cache size must be between {0} and {1} MB.")]
	SVC_004,

	[LSCoreValidationMessage("Unknown time zone.")]
	SVC_005,

	[LSCoreValidationMessage("Unknown chart live update mode.")]
	SVC_006,

	[LSCoreValidationMessage("Chart redraw interval must be between {0} and {1} ms.")]
	SVC_007,

	[LSCoreValidationMessage("This file is not a LimitlessTrading layout file.")]
	SVC_008,

	[LSCoreValidationMessage("The file is larger than {0} MB.")]
	SVC_009,

	[LSCoreValidationMessage(
		"This layout was exported by a newer version of LimitlessTrading (layout version {0}; this version reads up to {1}). Update the app to import it."
	)]
	SVC_010,

	[LSCoreValidationMessage("The layout file is damaged or incomplete.")]
	SVC_011,

	[LSCoreValidationMessage("Footprint imbalance must be between {0} and {1} %.")]
	SVC_012,
}
