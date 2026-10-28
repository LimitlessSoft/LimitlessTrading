namespace LT.Contracts.Constants;

/// <summary>The exported layout file (menu → Layout → Export / Import).</summary>
public static class LayoutFileConstants
{
	/// <summary>Marks an exported file as a layout (checked on import).</summary>
	public const string FileFormat = "LimitlessTrading.Layout";

	/// <summary>
	/// Version of the layout file written now. Raise it when the layout changes meaning; files of
	/// older versions must keep importing, newer ones are refused.
	/// </summary>
	public const int CurrentVersion = 1;

	public const string FileExtension = ".json";

	/// <summary>Suggested name of an exported layout.</summary>
	public const string DefaultFileName = "layout";

	/// <summary>Bigger files are not read on import.</summary>
	public const int MaxFileLength = 4 * 1024 * 1024;
}
