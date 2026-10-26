using LSCore.Validation.Contracts;

namespace LT.Contracts.Enums.ValidationCodes;

public enum PriceAlertValidationCodes
{
	[LSCoreValidationMessage("Pick a contract before adding an alert.")]
	PAVC_001,

	[LSCoreValidationMessage("An alert's price must be a number above 0.")]
	PAVC_002,

	[LSCoreValidationMessage("An alert's note can be at most {0} characters long.")]
	PAVC_003,

	[LSCoreValidationMessage(
		"The current price isn't known yet, so the alert's direction can't be told."
	)]
	PAVC_004,

	[LSCoreValidationMessage("At most {0} alerts can be active at a time.")]
	PAVC_005,

	[LSCoreValidationMessage(
		"Alerts can watch at most {0} contracts at a time (one market data line each)."
	)]
	PAVC_006,

	[LSCoreValidationMessage("That alert no longer exists.")]
	PAVC_007,
}
