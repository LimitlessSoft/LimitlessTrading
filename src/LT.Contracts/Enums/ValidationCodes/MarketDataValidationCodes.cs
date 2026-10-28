using LSCore.Validation.Contracts;

namespace LT.Contracts.Enums.ValidationCodes;

public enum MarketDataValidationCodes
{
	[LSCoreValidationMessage("Market data line limit reached ({0}).")]
	MDVC_001,

	[LSCoreValidationMessage("Not connected to TWS / IB Gateway.")]
	MDVC_002,

	[LSCoreValidationMessage("Tick-by-tick subscription limit reached ({0}).")]
	MDVC_003,

	[LSCoreValidationMessage("Quote snapshot limit reached ({0} at once).")]
	MDVC_004,

	[LSCoreValidationMessage("Quote snapshot timed out.")]
	MDVC_005,
}
