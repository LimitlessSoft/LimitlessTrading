using LT.Contracts.Dtos.Charts;
using LT.Contracts.Dtos.Indicators;

namespace LT.Contracts.Interfaces.IEngines;

/// <summary>
/// Runs indicators: the built-in ones and compiled script files (LT.Indicators.Scripting, the
/// only place that knows the SDK types). Nothing here throws for a bad script or a failing
/// indicator: compile errors come back on the definition, runtime errors on the result.
/// Thread-safe; computes on the calling thread.
/// </summary>
public interface IIndicatorEngine
{
	IReadOnlyList<IndicatorDefinitionDto> GetBuiltInDefinitions();

	/// <summary>
	/// Compiles <paramref name="source"/> as the script <paramref name="definitionId"/> and makes
	/// it the one that runs (the previous build is unloaded). An unchanged source reuses the
	/// last build. On errors the old build is dropped too, so a broken file never runs.
	/// </summary>
	IndicatorDefinitionDto CompileScript(
		string definitionId,
		string fileName,
		string filePath,
		string source,
		CancellationToken cancellationToken = default
	);

	/// <summary>Unloads a script whose file is gone.</summary>
	void RemoveScript(string definitionId);

	/// <summary>
	/// Computes each run over <paramref name="bars"/>, each within <paramref name="budget"/>.
	/// One result per run, in order; failures are in its <c>Error</c>.
	/// </summary>
	IReadOnlyList<IndicatorResultDto> Compute(
		IReadOnlyList<BarDto> bars,
		bool isIntraday,
		IReadOnlyList<IndicatorRunDto> runs,
		TimeSpan budget
	);

	/// <summary>The SDK assembly scripts compile against (copied into the scripts folder for editors).</summary>
	string GetSdkAssemblyPath();
}
