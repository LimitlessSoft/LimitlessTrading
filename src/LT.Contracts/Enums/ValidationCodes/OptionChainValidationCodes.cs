using LSCore.Validation.Contracts;

namespace LT.Contracts.Enums.ValidationCodes;

public enum OptionChainValidationCodes
{
	[LSCoreValidationMessage("Expiry must be YYYYMM or YYYYMMDD.")]
	OCVC_001,

	[LSCoreValidationMessage("Option chains are available for stocks and indices only.")]
	OCVC_002,

	[LSCoreValidationMessage("No options found for {0} expiring {1}.")]
	OCVC_003,
}
