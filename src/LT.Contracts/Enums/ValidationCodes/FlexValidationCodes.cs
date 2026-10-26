using LSCore.Validation.Contracts;

namespace LT.Contracts.Enums.ValidationCodes;

/// <summary>Flex settings validation and the Flex Web Service errors shown to the user.</summary>
public enum FlexValidationCodes
{
	[LSCoreValidationMessage("The Flex token is required when a query ID is set.")]
	FVC_001,

	[LSCoreValidationMessage(
		"The Flex token must be {0} to {1} letters or digits, as Client Portal shows it."
	)]
	FVC_002,

	[LSCoreValidationMessage("The Activity Flex query ID is required when a token is set.")]
	FVC_003,

	[LSCoreValidationMessage("A Flex query ID is up to {0} digits.")]
	FVC_004,

	[LSCoreValidationMessage(
		"The Flex token has expired. Generate a new one in Client Portal (Performance & Reports → Flex Queries → Flex Web Service Configuration) and paste it in Settings. (Flex 1012)"
	)]
	FVC_005,

	[LSCoreValidationMessage(
		"The Flex token is invalid. Copy it again from Client Portal → Flex Web Service Configuration. (Flex 1015)"
	)]
	FVC_006,

	[LSCoreValidationMessage(
		"IB refused the request from this IP address: the Flex token is restricted to another IP. Generate a token without an IP restriction. (Flex 1013)"
	)]
	FVC_007,

	[LSCoreValidationMessage(
		"The Flex query ID is invalid. Check the query ID next to the query in Client Portal → Flex Queries. (Flex 1014)"
	)]
	FVC_008,

	[LSCoreValidationMessage(
		"Too many Flex requests for this token (IB allows one per second, ten per minute). Trying again later. (Flex 1018)"
	)]
	FVC_009,

	[LSCoreValidationMessage(
		"The Flex Web Service is not active for this account. Turn it on in Client Portal → Flex Web Service Configuration. (Flex 1011)"
	)]
	FVC_010,

	[LSCoreValidationMessage(
		"IB couldn't validate the Flex request. Check the token and the query ID. (Flex 1020)"
	)]
	FVC_011,

	[LSCoreValidationMessage("IB has no Flex statement for this period. (Flex 1003)")]
	FVC_012,

	[LSCoreValidationMessage(
		"Legacy Flex queries are no longer supported by IB. Create an Activity Flex Query. (Flex 1010)"
	)]
	FVC_013,

	[LSCoreValidationMessage("IB Flex error {0}: {1}")]
	FVC_014,

	[LSCoreValidationMessage(
		"The Flex statement wasn't ready after {0} minutes. Trying again later."
	)]
	FVC_015,

	[LSCoreValidationMessage(
		"IB's Flex service answered with something that isn't a Flex statement."
	)]
	FVC_016,

	[LSCoreValidationMessage("Couldn't reach IB's Flex service: {0}")]
	FVC_017,

	[LSCoreValidationMessage(
		"The Activity query returned no Net Asset Value section. Add \"Net Asset Value (NAV) in Base\" to the query (see Settings → Account history)."
	)]
	FVC_018,

	[LSCoreValidationMessage(
		"The Trade Confirmation query returned no trade confirmations section. Use a Trade Confirmation Flex Query (not an Activity one) with the Trade Confirmations section."
	)]
	FVC_019,
}
