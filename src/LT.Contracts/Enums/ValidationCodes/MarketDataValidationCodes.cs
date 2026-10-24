using LSCore.Validation.Contracts;

namespace LT.Contracts.Enums.ValidationCodes;

public enum MarketDataValidationCodes
{
	[LSCoreValidationMessage("Market data line limit reached ({0}).")]
	MDVC_001,

	[LSCoreValidationMessage("Not connected to TWS / IB Gateway.")]
	MDVC_002,
}
