using LSCore.Validation.Contracts;

namespace LT.Contracts.Enums.ValidationCodes;

public enum GammaExposureValidationCodes
{
	[LSCoreValidationMessage("Expiration must be YYYYMMDD.")]
	GXVC_001,

	[LSCoreValidationMessage("Strikes around the spot must be between 1 and {0}.")]
	GXVC_002,

	[LSCoreValidationMessage("No market data lines free for the options (the limit is {0}).")]
	GXVC_003,

	[LSCoreValidationMessage("{0} has no price yet, so the strikes around it can't be picked.")]
	GXVC_004,

	[LSCoreValidationMessage("IB sent no greeks or open interest for the {0} options of {1}.")]
	GXVC_005,
}
