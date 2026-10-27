using LSCore.Validation.Contracts;

namespace LT.Contracts.Enums.ValidationCodes;

public enum CalendarValidationCodes
{
	[LSCoreValidationMessage("Pick the calendar's date range.")]
	CLVC_001,

	[LSCoreValidationMessage("Pick which events the calendar shows.")]
	CLVC_002,

	[LSCoreValidationMessage(
		"This IB account has no Wall Street Horizon subscription (IB error {0}: \"{1}\"). Subscribe to \"{2}\" in IBKR Client Portal → Settings → Market Data Subscriptions, log in to IB Gateway again, then press Refresh — or turn the source off in Settings → Calendar."
	)]
	CLVC_003,

	[LSCoreValidationMessage("IB refused the calendar request (IB error {0}: {1}).")]
	CLVC_004,

	[LSCoreValidationMessage(
		"Wall Street Horizon answered in a format the app can't read yet, so nothing is shown rather than a wrong list. IB sent: {0}"
	)]
	CLVC_005,

	[LSCoreValidationMessage("Not connected to IB Gateway.")]
	CLVC_006,

	[LSCoreValidationMessage(
		"Every calendar source is off. Turn on Nasdaq (no setup needed) or IB Wall Street Horizon in Settings → Calendar."
	)]
	CLVC_007,

	[LSCoreValidationMessage("Nasdaq's calendar could not be loaded ({0}).")]
	CLVC_008,

	[LSCoreValidationMessage(
		"Nasdaq's calendar answered in a format the app can't read, so nothing of it is shown rather than a wrong list. Nasdaq sent: {0}"
	)]
	CLVC_009,

	[LSCoreValidationMessage("Nasdaq refused the calendar request: {0}")]
	CLVC_010,
}
