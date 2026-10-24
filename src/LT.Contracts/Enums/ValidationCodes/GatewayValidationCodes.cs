using LSCore.Validation.Contracts;

namespace LT.Contracts.Enums.ValidationCodes;

public enum GatewayValidationCodes
{
	[LSCoreValidationMessage("Username is required.")]
	GVC_001,

	[LSCoreValidationMessage("Username can be at most {0} characters long.")]
	GVC_002,

	[LSCoreValidationMessage("Password is required.")]
	GVC_003,

	[LSCoreValidationMessage("Password can be at most {0} characters long.")]
	GVC_004,

	[LSCoreValidationMessage("Password cannot contain line breaks.")]
	GVC_005,

	[LSCoreValidationMessage("IBC folder is required.")]
	GVC_006,

	[LSCoreValidationMessage("Gateway folder is required.")]
	GVC_007,

	[LSCoreValidationMessage("Paths can be at most {0} characters long.")]
	GVC_008,

	[LSCoreValidationMessage(
		"Gateway version must be the major version digits, e.g. 1037 for 10.37."
	)]
	GVC_009,

	[LSCoreValidationMessage("Unknown trading mode.")]
	GVC_010,

	[LSCoreValidationMessage("IB Gateway is already running. Stop it first.")]
	GVC_011,

	[LSCoreValidationMessage(
		"IBC start script not found at {0}. Use Locate or Browse… under Install."
	)]
	GVC_012,

	[LSCoreValidationMessage(
		"Port {0} is already in use — another TWS / Gateway is probably running."
	)]
	GVC_013,

	[LSCoreValidationMessage("IB Gateway did not open the API port within {0} minutes.")]
	GVC_014,

	[LSCoreValidationMessage("IB Gateway rejected the login — check username and password.")]
	GVC_015,

	[LSCoreValidationMessage("IBC exited with code {0} before login completed. {1}")]
	GVC_016,

	[LSCoreValidationMessage("IB Gateway exited unexpectedly (code {0}).")]
	GVC_017,

	[LSCoreValidationMessage("No IBC install in {0} — expected scripts/{1} inside it.")]
	GVC_018,

	[LSCoreValidationMessage(
		"No IB Gateway install in {0} — expected an ibgateway/<version> folder inside it."
	)]
	GVC_019,

	[LSCoreValidationMessage("IBC release {0} has no download for this OS (expected {1}*.zip).")]
	GVC_020,

	[LSCoreValidationMessage("Downloaded IBC has no scripts/{0} — the release layout changed.")]
	GVC_021,
}
