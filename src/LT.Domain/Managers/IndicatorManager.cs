using LSCore.Exceptions;
using LSCore.Validation.Contracts;
using LSCore.Validation.Domain;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Indicators;
using LT.Contracts.Enums;
using LT.Contracts.Enums.ValidationCodes;
using LT.Contracts.Helpers;
using LT.Contracts.Interfaces.IEngines;
using LT.Contracts.Interfaces.IManagers;
using LT.Contracts.Interfaces.IRepositories;
using LT.Contracts.Requests.Indicators;

namespace LT.Domain.Managers;

/// <summary>
/// The indicator catalog and computing. Built-ins come from the engine; scripts are the files of
/// <see cref="IIndicatorScriptRepository"/>, compiled one at a time on the thread pool at start
/// and whenever the folder reports a change (compile errors go to the definition and the
/// message log, never to the chart). Registered as a singleton.
/// </summary>
public class IndicatorManager(
	IIndicatorEngine engine,
	IIndicatorScriptRepository repository,
	IMessageLogManager messageLogManager
) : IIndicatorManager
{
	private readonly Lock _lock = new();
	private readonly Dictionary<string, IndicatorDefinitionDto> _scripts = [];
	private readonly SemaphoreSlim _compile = new(1, 1);
	private IReadOnlyList<IndicatorDefinitionDto>? _builtIns;

	public event Action<IReadOnlyCollection<string>>? DefinitionsChanged;

	private IReadOnlyList<IndicatorDefinitionDto> BuiltIns =>
		_builtIns ??= engine.GetBuiltInDefinitions();

	public IReadOnlyList<IndicatorDefinitionDto> GetDefinitions()
	{
		lock (_lock)
			return
			[
				.. BuiltIns,
				.. _scripts.Values.OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase),
			];
	}

	public IndicatorDefinitionDto? GetDefinition(string definitionId)
	{
		lock (_lock)
			return _scripts.TryGetValue(definitionId, out var script)
				? script
				: BuiltIns.FirstOrDefault(x => x.Id == definitionId);
	}

	public string GetScriptsDirectory() => repository.GetDirectory();

	public async Task RunAsync(CancellationToken stoppingToken)
	{
		try
		{
			repository.EnsureWorkspace(
				IndicatorWorkspaceHelpers.GetFiles(
					engine.GetSdkAssemblyPath(),
					Environment.Version.Major
				)
			);
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			messageLogManager.Add(
				MessageSeverity.Warning,
				MessageLogConstants.SourceIndicators,
				$"Couldn't set up the indicators folder {repository.GetDirectory()}: {ex.Message}"
			);
		}

		repository.Changed += OnFilesChanged;
		try
		{
			repository.StartWatching();
			var files = repository.GetAll().Select(x => x.FileName).ToList();
			await ReloadAsync(files, stoppingToken);
			await Task.Delay(Timeout.Infinite, stoppingToken);
		}
		catch (OperationCanceledException) { }
		finally
		{
			repository.Changed -= OnFilesChanged;
		}
	}

	public IndicatorInstanceDto CreateInstance(string definitionId)
	{
		var definition =
			GetDefinition(definitionId)
			?? throw new LSCoreBadRequestException(
				IndicatorValidationCodes.IVC_001.GetValidationMessage(definitionId)
			);
		return new IndicatorInstanceDto
		{
			DefinitionId = definition.Id,
			Inputs = IndicatorHelpers.GetDefaultInputs(definition),
		};
	}

	public IndicatorInstanceDto UpdateInputs(UpdateIndicatorInputsRequest request)
	{
		request.Validate();
		return new IndicatorInstanceDto
		{
			Id = request.Instance.Id,
			DefinitionId = request.Instance.DefinitionId,
			Inputs = new Dictionary<string, double>(request.Inputs),
			IsVisible = request.Instance.IsVisible,
			PlotColors = request.Instance.PlotColors,
		};
	}

	public IReadOnlyList<IndicatorResultDto> Compute(ComputeIndicatorsRequest request)
	{
		request.Validate();
		var results = new IndicatorResultDto[request.Indicators.Count];
		var runs = new List<(int Index, IndicatorRunDto Run)>();
		for (var i = 0; i < request.Indicators.Count; i++)
		{
			var instance = request.Indicators[i];
			var definition = GetDefinition(instance.DefinitionId);
			var inputs =
				definition != null
					? IndicatorHelpers.ResolveInputs(definition, instance)
					: instance.Inputs;
			results[i] = new IndicatorResultDto
			{
				InstanceId = instance.Id,
				DefinitionId = instance.DefinitionId,
				Label =
					definition != null
						? IndicatorHelpers.GetLabel(definition, inputs)
						: IndicatorHelpers.GetMissingLabel(instance.DefinitionId),
				Pane = definition?.Pane ?? IndicatorPane.Overlay,
				IsVisible = instance.IsVisible,
				BarCount = request.Bars.Count,
				Error = definition switch
				{
					null => IndicatorValidationCodes.IVC_001.GetValidationMessage(
						IndicatorHelpers.GetMissingLabel(instance.DefinitionId)
					),
					_ => definition.Error,
				},
			};
			// A script compiling for the first time has nothing to run yet; a recompiling one
			// keeps running its last build until the new one replaces it
			if (
				definition is { Error: null }
				&& instance.IsVisible
				&& !IndicatorHelpers.IsFirstCompile(definition)
			)
				runs.Add(
					(i, new IndicatorRunDto { DefinitionId = definition.Id, Inputs = inputs })
				);
		}

		var computed = engine.Compute(
			request.Bars,
			request.IsIntraday,
			[.. runs.Select(x => x.Run)],
			IndicatorConstants.ComputeBudget
		);
		for (var k = 0; k < runs.Count; k++)
		{
			var result = results[runs[k].Index];
			var output = computed[k];
			result.Pane = output.Pane;
			result.ScaleMin = output.ScaleMin;
			result.ScaleMax = output.ScaleMax;
			result.Decimals = output.Decimals;
			result.Plots = output.Plots;
			result.Error = output.Error;
			IndicatorChartHelpers.ApplyPlotColors(
				result.Plots,
				request.Indicators[runs[k].Index].PlotColors
			);
		}
		return results;
	}

	private void OnFilesChanged(IReadOnlyCollection<string> fileNames) =>
		_ = ReloadAsync(fileNames, CancellationToken.None);

	// One compile at a time; each reads the file as it is then, so the last change wins
	private async Task ReloadAsync(IReadOnlyCollection<string> fileNames, CancellationToken token)
	{
		var ids = fileNames.Select(IndicatorHelpers.GetScriptId).ToList();
		lock (_lock)
			foreach (var id in ids)
				_scripts[id] = IndicatorHelpers.GetCompiling(_scripts.GetValueOrDefault(id), id);
		DefinitionsChanged?.Invoke(ids);

		await _compile.WaitAsync(token);
		try
		{
			foreach (var fileName in fileNames)
			{
				token.ThrowIfCancellationRequested();
				await Task.Run(() => Reload(fileName, token), token);
			}
		}
		finally
		{
			_compile.Release();
		}
	}

	private void Reload(string fileName, CancellationToken token)
	{
		var id = IndicatorHelpers.GetScriptId(fileName);
		var script = repository.Get(fileName);
		if (script == null)
		{
			engine.RemoveScript(id);
			lock (_lock)
				_scripts.Remove(id);
			DefinitionsChanged?.Invoke([id]);
			return;
		}

		IndicatorDefinitionDto definition;
		try
		{
			definition = engine.CompileScript(id, fileName, script.FilePath, script.Source, token);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			definition = new IndicatorDefinitionDto
			{
				Id = id,
				Name = Path.GetFileNameWithoutExtension(fileName),
				ShortName = Path.GetFileNameWithoutExtension(fileName),
				Source = IndicatorSource.Script,
				FilePath = script.FilePath,
				Error = ex.Message,
			};
		}

		IndicatorDefinitionDto? previous;
		lock (_lock)
		{
			_scripts.TryGetValue(id, out previous);
			definition.Revision = (previous?.Revision ?? 0) + 1;
			_scripts[id] = definition;
		}
		if (definition.Error != null && definition.Error != previous?.Error)
			messageLogManager.Add(
				MessageSeverity.Warning,
				MessageLogConstants.SourceIndicators,
				$"{fileName}: {definition.Error}"
			);
		DefinitionsChanged?.Invoke([id]);
	}
}
