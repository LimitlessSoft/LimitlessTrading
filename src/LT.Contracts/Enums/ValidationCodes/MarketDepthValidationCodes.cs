using LSCore.Validation.Contracts;

namespace LT.Contracts.Enums.ValidationCodes;

public enum MarketDepthValidationCodes
{
	[LSCoreValidationMessage("Ask for between 1 and {0} rows of depth.")]
	MDPVC_001,

	[LSCoreValidationMessage(
		"IB allows {0} market depth subscriptions at a time; close a depth window here, in TWS or in another app first."
	)]
	MDPVC_002,

	[LSCoreValidationMessage(
		"No market depth subscription for {0} (IB error {1}). Subscribe to the exchange's depth (Level 2) in IB's Market Data Subscriptions, or use SMART depth."
	)]
	MDPVC_003,
}
