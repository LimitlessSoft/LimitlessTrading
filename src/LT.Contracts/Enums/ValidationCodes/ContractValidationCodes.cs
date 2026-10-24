using LSCore.Validation.Contracts;

namespace LT.Contracts.Enums.ValidationCodes;

public enum ContractValidationCodes
{
	[LSCoreValidationMessage("Symbol is required.")]
	CTVC_001,

	[LSCoreValidationMessage("Symbol can be at most {0} characters long.")]
	CTVC_002,

	[LSCoreValidationMessage("Security type must be one of: {0}.")]
	CTVC_003,

	[LSCoreValidationMessage("Contract must be resolved (have a contract id).")]
	CTVC_004,

	[LSCoreValidationMessage("No stock found for \"{0}\".")]
	CTVC_005,
}
