namespace LT.Contracts.Constants;

/// <summary>Watchlist custom columns and watchlist files (export / import).</summary>
public static class WatchlistConstants
{
	/// <summary>A watchlist has at most this many custom columns.</summary>
	public const int MaxCustomColumns = 10;

	public const int ColumnNameMaxLength = 32;

	/// <summary>A custom column's cell holds at most this many characters.</summary>
	public const int CellValueMaxLength = 200;

	/// <summary>New custom columns are "Column 1", "Column 2" …</summary>
	public const string NewColumnNamePrefix = "Column";

	/// <summary>Marks an exported file as a watchlist (checked on import).</summary>
	public const string FileFormat = "LimitlessTrading.Watchlist";

	/// <summary>
	/// Version of the watchlist file written now. Raise it when a field changes meaning; files of
	/// older versions must keep importing.
	/// </summary>
	public const int FileVersion = 1;

	public const string FileExtension = ".json";

	/// <summary>Bigger files are not read on import.</summary>
	public const int MaxFileLength = 1024 * 1024;

	/// <summary>An imported list of symbols with no usable file name is called this.</summary>
	public const string ImportedWatchlistName = "Imported";
}
