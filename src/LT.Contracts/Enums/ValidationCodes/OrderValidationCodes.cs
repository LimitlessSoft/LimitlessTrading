using LSCore.Validation.Contracts;

namespace LT.Contracts.Enums.ValidationCodes;

public enum OrderValidationCodes
{
	[LSCoreValidationMessage("Pick a contract first (e.g. in the watchlist).")]
	OVC_001,

	[LSCoreValidationMessage("Pick an account.")]
	OVC_002,

	[LSCoreValidationMessage("Enter a quantity or an amount.")]
	OVC_003,

	[LSCoreValidationMessage("Enter a quantity or an amount, not both.")]
	OVC_004,

	[LSCoreValidationMessage("Quantity must be more than 0.")]
	OVC_005,

	[LSCoreValidationMessage("An amount can only be spent with a market order.")]
	OVC_006,

	[LSCoreValidationMessage("Enter a limit price above 0.")]
	OVC_007,

	[LSCoreValidationMessage("{0} orders can only use: {1}.")]
	OVC_008,

	[LSCoreValidationMessage("Amount must be between {0} and {1}.")]
	OVC_009,

	[LSCoreValidationMessage("The position is flat: there is nothing to exit.")]
	OVC_010,

	[LSCoreValidationMessage("An exit of this position is already working.")]
	OVC_011,

	[LSCoreValidationMessage("Set a one-click quantity of {0} to {1} whole contracts.")]
	OVC_012,
}
