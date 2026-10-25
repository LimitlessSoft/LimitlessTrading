using LSCore.Validation.Contracts;

namespace LT.Contracts.Enums.ValidationCodes;

public enum UpdateValidationCodes
{
	[LSCoreValidationMessage(
		"This copy is built from source: update it with git pull and rebuild."
	)]
	UVC_001,

	[LSCoreValidationMessage(
		"Installing updates from the app works on Linux release builds for now: download it from the release page."
	)]
	UVC_002,

	[LSCoreValidationMessage(
		"Can't write to the app folder {0}: download the update from the release page."
	)]
	UVC_003,

	[LSCoreValidationMessage("The release has no download for this system ({0}).")]
	UVC_004,

	[LSCoreValidationMessage("The download doesn't match the release's checksum: try again.")]
	UVC_005,

	[LSCoreValidationMessage("The download doesn't contain {0}.")]
	UVC_006,
}
