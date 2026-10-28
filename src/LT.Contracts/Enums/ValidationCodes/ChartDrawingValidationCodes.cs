using LSCore.Validation.Contracts;

namespace LT.Contracts.Enums.ValidationCodes;

public enum ChartDrawingValidationCodes
{
	[LSCoreValidationMessage("Pick a contract before drawing on the chart.")]
	CDVC_001,

	[LSCoreValidationMessage("That drawing tool isn't known.")]
	CDVC_002,

	[LSCoreValidationMessage("This drawing doesn't have the points its tool needs.")]
	CDVC_003,

	[LSCoreValidationMessage("A drawing's points must have a time and a price.")]
	CDVC_004,

	[LSCoreValidationMessage("Type the text of the note.")]
	CDVC_005,

	[LSCoreValidationMessage("A note can be at most {0} characters long.")]
	CDVC_006,

	[LSCoreValidationMessage(
		"A chart holds at most {0} drawings per symbol. Remove some to add more."
	)]
	CDVC_007,

	[LSCoreValidationMessage("At most {0} drawings can be kept across all symbols.")]
	CDVC_008,

	[LSCoreValidationMessage("That drawing no longer exists.")]
	CDVC_009,
}
