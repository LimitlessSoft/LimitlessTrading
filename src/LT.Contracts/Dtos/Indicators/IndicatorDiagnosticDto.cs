namespace LT.Contracts.Dtos.Indicators;

/// <summary>A compile error or warning of a script, at its line and column (1-based).</summary>
public class IndicatorDiagnosticDto
{
	public int Line { get; set; }
	public int Column { get; set; }
	public string Message { get; set; } = string.Empty;
	public bool IsError { get; set; } = true;

	public override string ToString() => Line > 0 ? $"line {Line}: {Message}" : Message;
}
