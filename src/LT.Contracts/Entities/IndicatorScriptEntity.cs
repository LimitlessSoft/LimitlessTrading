namespace LT.Contracts.Entities;

/// <summary>A script file in the indicators folder.</summary>
public class IndicatorScriptEntity
{
	/// <summary>File name with extension ("ema-cross.cs"); unique in the folder.</summary>
	public string FileName { get; set; } = string.Empty;

	public string FilePath { get; set; } = string.Empty;
	public string Source { get; set; } = string.Empty;
	public DateTimeOffset ModifiedAt { get; set; }
}
