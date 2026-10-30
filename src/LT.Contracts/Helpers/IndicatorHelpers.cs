using System.Globalization;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Indicators;
using LT.Contracts.Enums;

namespace LT.Contracts.Helpers;

public static class IndicatorHelpers
{
	public static string GetBuiltInId(string key) => IndicatorConstants.BuiltInIdPrefix + key;

	public static string GetScriptId(string fileName) =>
		IndicatorConstants.ScriptIdPrefix + fileName;

	public static bool IsScriptId(string definitionId) =>
		definitionId.StartsWith(IndicatorConstants.ScriptIdPrefix, StringComparison.Ordinal);

	/// <summary>"ema-cross.cs" of "script:ema-cross.cs"; null for a built-in id.</summary>
	public static string? GetScriptFileName(string definitionId) =>
		IsScriptId(definitionId) ? definitionId[IndicatorConstants.ScriptIdPrefix.Length..] : null;

	/// <summary>The indicators of a new chart: the volume bars (the volume indicator, no inputs).</summary>
	public static List<IndicatorInstanceDto> GetNewChartIndicators() =>
		[new IndicatorInstanceDto { DefinitionId = IndicatorConstants.VolumeDefinitionId }];

	/// <summary>
	/// Indicators saved before volume was an indicator (charts always drew the volume bars then),
	/// with the volume indicator put first so the bars stay; as they are when they already have
	/// it or are full (<see cref="IndicatorConstants.MaxIndicatorsPerChart"/>). Done once per
	/// saved chart or template, so a later removal sticks.
	/// </summary>
	public static List<IndicatorInstanceDto> AddVolume(IEnumerable<IndicatorInstanceDto> indicators)
	{
		var list = indicators.ToList();
		if (
			list.Count >= IndicatorConstants.MaxIndicatorsPerChart
			|| list.Any(x => x.DefinitionId == IndicatorConstants.VolumeDefinitionId)
		)
			return list;
		return [.. GetNewChartIndicators(), .. list];
	}

	/// <summary>Whether <paramref name="value"/> is allowed for <paramref name="input"/> as it is.</summary>
	public static bool IsValidInputValue(IndicatorInputDto input, double value) =>
		double.IsFinite(value)
		&& input.Type switch
		{
			IndicatorInputType.Bool => value is 0 or 1,
			IndicatorInputType.Int => value == Math.Round(value) && IsInRange(input, value),
			_ => IsInRange(input, value),
		};

	public static bool IsInRange(IndicatorInputDto input, double value) =>
		(input.Min is not { } min || value >= min) && (input.Max is not { } max || value <= max);

	public static Dictionary<string, double> GetDefaultInputs(IndicatorDefinitionDto definition) =>
		definition.Inputs.ToDictionary(x => x.Name, x => x.DefaultValue);

	/// <summary>
	/// The values a compute uses: the definition's inputs, each the instance's value when it is
	/// still valid (a script may have changed its inputs since it was saved), else the default.
	/// </summary>
	public static Dictionary<string, double> ResolveInputs(
		IndicatorDefinitionDto definition,
		IndicatorInstanceDto instance
	) =>
		definition.Inputs.ToDictionary(
			x => x.Name,
			x =>
				instance.Inputs.TryGetValue(x.Name, out var value) && IsValidInputValue(x, value)
					? value
					: x.DefaultValue
		);

	/// <summary>Legend text: the short name and the numeric inputs ("BB 20 2").</summary>
	public static string GetLabel(
		IndicatorDefinitionDto definition,
		IReadOnlyDictionary<string, double> inputs
	)
	{
		var values = definition
			.Inputs.Where(x => x.Type != IndicatorInputType.Bool)
			.Select(x =>
				(inputs.TryGetValue(x.Name, out var v) ? v : x.DefaultValue).ToString(
					"0.####",
					CultureInfo.InvariantCulture
				)
			)
			.ToList();
		var name = string.IsNullOrWhiteSpace(definition.ShortName)
			? definition.Name
			: definition.ShortName;
		return values.Count == 0 ? name : $"{name} {string.Join(' ', values)}";
	}

	/// <summary>Legend text of an instance whose definition is gone or not loaded yet.</summary>
	public static string GetMissingLabel(string definitionId) =>
		GetScriptFileName(definitionId) is { } file
			? Path.GetFileNameWithoutExtension(file)
			: definitionId;

	/// <summary>The first error of a definition, as one line ("line 3: ; expected").</summary>
	public static string DescribeErrors(IReadOnlyList<IndicatorDiagnosticDto> diagnostics)
	{
		var errors = diagnostics.Where(x => x.IsError).ToList();
		if (errors.Count == 0)
			return "Compile failed.";
		var first = errors[0].ToString();
		return errors.Count == 1 ? first : $"{first} (+{errors.Count - 1} more)";
	}

	/// <summary>
	/// <paramref name="current"/> marked as compiling (a copy: definitions handed out are never
	/// changed), or a placeholder named after the file for a script not compiled before.
	/// </summary>
	public static IndicatorDefinitionDto GetCompiling(
		IndicatorDefinitionDto? current,
		string definitionId
	)
	{
		var name = GetMissingLabel(definitionId);
		return current == null
			? new IndicatorDefinitionDto
			{
				Id = definitionId,
				Name = name,
				ShortName = name,
				Source = IndicatorSource.Script,
				IsCompiling = true,
				Revision = 0,
			}
			: new IndicatorDefinitionDto
			{
				Id = current.Id,
				Name = current.Name,
				ShortName = current.ShortName,
				Source = current.Source,
				Pane = current.Pane,
				Inputs = current.Inputs,
				FilePath = current.FilePath,
				IsCompiling = true,
				Error = current.Error,
				Diagnostics = current.Diagnostics,
				Revision = current.Revision,
			};
	}

	/// <summary>A script being compiled for the first time: nothing of it can run yet.</summary>
	public static bool IsFirstCompile(IndicatorDefinitionDto definition) =>
		definition is { IsCompiling: true, Revision: 0, Source: IndicatorSource.Script };
}
