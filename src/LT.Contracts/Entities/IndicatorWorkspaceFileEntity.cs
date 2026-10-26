namespace LT.Contracts.Entities;

/// <summary>A file the indicators folder is set up with (editor project, SDK copy, example).</summary>
public class IndicatorWorkspaceFileEntity
{
	/// <summary>Path inside the indicators folder ("indicators.csproj", ".sdk/LT.Indicators.Sdk.dll").</summary>
	public string RelativePath { get; set; } = string.Empty;

	/// <summary>Text to write; null with <see cref="CopyFrom"/>.</summary>
	public string? Content { get; set; }

	/// <summary>File to copy (when it differs from the one there).</summary>
	public string? CopyFrom { get; set; }

	/// <summary>Written only when the folder is created (the example, the README).</summary>
	public bool FirstRunOnly { get; set; }

	/// <summary>Written only when missing (the editor project, which the user may change).</summary>
	public bool KeepExisting { get; set; }
}
