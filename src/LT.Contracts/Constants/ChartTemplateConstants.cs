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

	/// <summary>One import file holds at most this many templates.</summary>
	public const int MaxTemplatesPerFile = 200;

	/// <summary>Bigger files are not read on import.</summary>
	public const int MaxFileLength = 2 * 1024 * 1024;
}
