using LSCore.Validation.Contracts;

namespace LT.Contracts.Enums.ValidationCodes;

/// <summary>Not persisted. Codes of the IBC-era log-in form (credentials, IBC) were removed.</summary>
public enum GatewayValidationCodes
{
	[LSCoreValidationMessage("Gateway folder is required.")]
	GVC_007,

	[LSCoreValidationMessage("Paths can be at most {0} characters long.")]
	GVC_008,

	[LSCoreValidationMessage(
		"Gateway version must be the major version digits, e.g. 1037 for 10.37."
	)]
	GVC_009,

	[LSCoreValidationMessage("IB Gateway is already running. Stop it first.")]
	GVC_011,

	[LSCoreValidationMessage(
		"IB Gateway did not open the API port within {0} minutes. In Gateway, Configure → Settings → API → Settings must keep the socket port at 4001 (live) or 4002 (paper)."
	)]
	GVC_014,

	[LSCoreValidationMessage("IB Gateway exited unexpectedly (code {0}).")]
	GVC_017,

	[LSCoreValidationMessage(
		"No IB Gateway install in {0} — expected an ibgateway/<version> folder inside it."
	)]
	GVC_019,

	[LSCoreValidationMessage(
		"No IB Gateway {1} launcher found in {0}. Use Locate or Browse… to pick the install."
	)]
	GVC_022,

	[LSCoreValidationMessage("IB Gateway closed with code {0} before the login finished.")]
	GVC_023,
}
