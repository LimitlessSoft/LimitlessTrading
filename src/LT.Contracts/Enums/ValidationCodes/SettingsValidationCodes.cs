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
}
