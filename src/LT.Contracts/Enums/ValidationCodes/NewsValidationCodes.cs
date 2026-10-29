using LSCore.Validation.Contracts;

namespace LT.Contracts.Enums.ValidationCodes;

public enum NewsValidationCodes
{
	[LSCoreValidationMessage("Pick at least one news provider.")]
	NVC_001,

	[LSCoreValidationMessage("Ask for between 1 and {0} headlines at a time.")]
	NVC_002,

	[LSCoreValidationMessage("None of the selected providers has a broad tape.")]
	NVC_003,

	[LSCoreValidationMessage("No news source can serve this request.")]
	NVC_004,

	[LSCoreValidationMessage("The article is not available.")]
	NVC_005,

	[LSCoreValidationMessage("Pick a symbol to see its news.")]
	NVC_006,

	[LSCoreValidationMessage("Type at least {0} characters to search the news archive.")]
	NVC_007,

	[LSCoreValidationMessage("Couldn't read the {0} feed: {1}")]
	NVC_008,
}
