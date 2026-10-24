using LSCore.Validation.Contracts;

namespace LT.Contracts.Enums.ValidationCodes;

public enum WatchlistValidationCodes
{
	[LSCoreValidationMessage("{0} is already in the watchlist.")]
	WVC_001,

	[LSCoreValidationMessage("Watchlist can hold at most {0} contracts.")]
	WVC_002,

	[LSCoreValidationMessage("Watchlist name is required.")]
	WVC_003,

	[LSCoreValidationMessage("Watchlist name can be at most {0} characters long.")]
	WVC_004,

	[LSCoreValidationMessage("A watchlist named {0} already exists.")]
	WVC_005,

	[LSCoreValidationMessage("The last watchlist can't be deleted.")]
	WVC_006,

	[LSCoreValidationMessage("That watchlist no longer exists.")]
	WVC_007,
}
