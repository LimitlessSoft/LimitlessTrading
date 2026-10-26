using LT.Contracts.Enums;

namespace LT.Contracts.Dtos.Indicators;

/// <summary>An indicator that can be added to a chart: a built-in one or a script file.</summary>
public class IndicatorDefinitionDto
{
	/// <summary>"builtin:rsi" or "script:&lt;file name&gt;".</summary>
	public string Id { get; set; } = string.Empty;
	public string Name { get; set; } = string.Empty;

	/// <summary>Legend name before the input values ("RSI").</summary>
	public string ShortName { get; set; } = string.Empty;

	public IndicatorSource Source { get; set; }
	public IndicatorPane Pane { get; set; }
	public List<IndicatorInputDto> Inputs { get; set; } = [];

	/// <summary>Scripts: the file's full path.</summary>
	public string? FilePath { get; set; }

	/// <summary>Scripts: being compiled (first load or after a change).</summary>
	public bool IsCompiling { get; set; }

	/// <summary>Why it can't run (compile errors, banned API, a failing describe run); null when it can.</summary>
	public string? Error { get; set; }

	public List<IndicatorDiagnosticDto> Diagnostics { get; set; } = [];

	/// <summary>Scripts: how many times it has been compiled (0: not yet).</summary>
	public int Revision { get; set; }
}
