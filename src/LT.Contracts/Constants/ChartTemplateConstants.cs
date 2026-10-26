namespace LT.Contracts.Constants;

public static class ChartTemplateConstants
{
	/// <summary>
	/// Version of the template format written now. Raise it when a setting changes meaning;
	/// templates and files of older versions must keep loading.
	/// </summary>
	public const int CurrentVersion = 1;

	/// <summary>Marks an exported file as chart templates (checked on import).</summary>
	public const string FileFormat = "LimitlessTrading.ChartTemplates";

	public const string FileExtension = ".json";

	/// <summary>Suggested name of a file exporting every template.</summary>
	public const string AllTemplatesFileName = "chart-templates";

	public const int NameMaxLength = 64;

	/// <summary>
	/// A chart following its template saves its changes to it once they stop for this long (a
	/// right-margin drag is one save).
	/// </summary>
	public static readonly TimeSpan FollowSaveDebounce = TimeSpan.FromMilliseconds(400);

	/// <summary>Names the template in a settings error when only its id is known.</summary>
	public const string UnnamedTemplateLabel = "Template";

	/// <summary>One import file holds at most this many templates.</summary>
	public const int MaxTemplatesPerFile = 200;

	/// <summary>Bigger files are not read on import.</summary>
	public const int MaxFileLength = 2 * 1024 * 1024;
}
