using LSCore.Validation.Contracts;

namespace LT.Contracts.Enums.ValidationCodes;

public enum ConnectionValidationCodes
{
	[LSCoreValidationMessage("Host is required.")]
	CVC_001,

	[LSCoreValidationMessage("Host can be at most {0} characters long.")]
	CVC_002,

	[LSCoreValidationMessage("Port must be between {0} and {1}.")]
	CVC_003,

	[LSCoreValidationMessage("Client id must be between {0} and {1}.")]
	CVC_004,

	[LSCoreValidationMessage("Already connected. Disconnect first.")]
	CVC_005,
}
