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

	[LSCoreValidationMessage("{0} is not in the watchlist.")]
	WVC_008,

	[LSCoreValidationMessage("Column name is required.")]
	WVC_009,

	[LSCoreValidationMessage("Column name can be at most {0} characters long.")]
	WVC_010,

	[LSCoreValidationMessage("A column named {0} already exists.")]
	WVC_011,

	[LSCoreValidationMessage("A watchlist can have at most {0} custom columns.")]
	WVC_012,

	[LSCoreValidationMessage("That column no longer exists.")]
	WVC_013,

	[LSCoreValidationMessage("A value can be at most {0} characters long.")]
	WVC_014,

	[LSCoreValidationMessage("This file is not a watchlist file or a list of symbols.")]
	WVC_015,

	[LSCoreValidationMessage("The file has no symbols.")]
	WVC_016,

	[LSCoreValidationMessage("The file is larger than {0} MB.")]
	WVC_017,

	[LSCoreValidationMessage("{0} is not a symbol.")]
	WVC_018,

	[LSCoreValidationMessage("None of the symbols were found: {0}.")]
	WVC_019,

	[LSCoreValidationMessage("Connect to IB to import a list of symbols.")]
	WVC_020,

	[LSCoreValidationMessage("{0} in the file has no contract id.")]
	WVC_021,
}
