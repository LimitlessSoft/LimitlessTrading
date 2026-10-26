using LT.Contracts.Dtos.Indicators;
using LT.Contracts.Requests.Indicators;

namespace LT.Contracts.Interfaces.IManagers;

/// <summary>
/// The chart indicators: the catalog (built-ins and the script files, compiled in the
/// background and again whenever a file changes), new instances, their inputs and computing a
/// chart's indicators over its bars.
/// </summary>
public interface IIndicatorManager
{
	/// <summary>Ids of definitions that were added, recompiled or removed. Raised on any thread.</summary>
	event Action<IReadOnlyCollection<string>>? DefinitionsChanged;

	/// <summary>Built-ins first, then scripts by name.</summary>
	IReadOnlyList<IndicatorDefinitionDto> GetDefinitions();

	IndicatorDefinitionDto? GetDefinition(string definitionId);

	/// <summary>The scripts folder (created on start).</summary>
	string GetScriptsDirectory();

	/// <summary>
	/// Sets up the scripts folder, compiles every script and keeps watching the folder until
	/// <paramref name="stoppingToken"/> is cancelled. Run once, by the app's host.
	/// </summary>
	Task RunAsync(CancellationToken stoppingToken);

	/// <summary>A new instance with the definition's default inputs.</summary>
	IndicatorInstanceDto CreateInstance(string definitionId);

	/// <summary>Validates the inputs against the definition and returns the updated instance.</summary>
	IndicatorInstanceDto UpdateInputs(UpdateIndicatorInputsRequest request);

	/// <summary>
	/// One result per instance, in order (hidden ones only with their label). Synchronous and
	/// CPU-bound: call it off the UI thread.
	/// </summary>
	IReadOnlyList<IndicatorResultDto> Compute(ComputeIndicatorsRequest request);
}
