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

	[LSCoreValidationMessage("A drawing's colour is a hex colour like #2962FF.")]
	CDVC_010,

	[LSCoreValidationMessage("A drawing's lines are {0} to {1} pixels wide.")]
	CDVC_011,

	[LSCoreValidationMessage("That line style isn't known.")]
	CDVC_012,

	[LSCoreValidationMessage("A Fibonacci drawing has 1 to {0} levels.")]
	CDVC_013,

	[LSCoreValidationMessage("A Fibonacci level is a number from {0} to {1}.")]
	CDVC_014,

	[LSCoreValidationMessage("Each Fibonacci level can be there only once.")]
	CDVC_015,
}
