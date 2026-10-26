namespace LT.Contracts.Constants;

public static class IndicatorConstants
{
	/// <summary>Script folder in the app-data folder: one indicator per <c>*.cs</c> file.</summary>
	public const string DirectoryName = "indicators";

	/// <summary>Copy of the SDK assembly inside the script folder, for the editor project.</summary>
	public const string SdkDirectoryName = ".sdk";

	public const string SdkAssemblyFileName = "LT.Indicators.Sdk.dll";
	public const string SdkDocumentationFileName = "LT.Indicators.Sdk.xml";
	public const string ProjectFileName = "indicators.csproj";
	public const string ReadmeFileName = "README.md";
	public const string ExampleFileName = "ema-cross.cs";
	public const string ScriptExtension = ".cs";

	public const string BuiltInIdPrefix = "builtin:";
	public const string ScriptIdPrefix = "script:";

	/// <summary>One indicator's compute (or its describe run) is stopped after this long.</summary>
	public static readonly TimeSpan ComputeBudget = TimeSpan.FromSeconds(2);

	/// <summary>File changes within this window are reloaded once (editors write in steps).</summary>
	public static readonly TimeSpan ReloadDebounce = TimeSpan.FromMilliseconds(300);

	/// <summary>
	/// How often the folder is scanned when the file watcher can't run (inotify limit, network
	/// drive) or lost events.
	/// </summary>
	public static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);

	/// <summary>A chart recomputes at most this often while bars stream in.</summary>
	public static readonly TimeSpan ComputeDebounce = TimeSpan.FromMilliseconds(200);

	public const int MaxIndicatorsPerChart = 16;

	/// <summary>Bigger script files are not compiled.</summary>
	public const int MaxScriptLength = 256 * 1024;

	/// <summary>Compile diagnostics kept per script.</summary>
	public const int MaxDiagnostics = 50;

	/// <summary>Namespaces every script sees (the compiler and the editor project use the same).</summary>
	public static readonly IReadOnlyList<string> ScriptUsings =
	[
		"System",
		"System.Collections.Generic",
		"System.Linq",
		"LT.Indicators.Sdk",
	];

	/// <summary>Imported with <c>using static</c>: Close, Input, Plot … without a class around them.</summary>
	public const string ScriptStaticUsing = "LT.Indicators.Sdk.Script";
}
