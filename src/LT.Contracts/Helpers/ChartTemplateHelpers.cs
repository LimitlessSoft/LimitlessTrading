using System.Text.Json;
using System.Text.Json.Serialization;
using LSCore.Validation.Contracts;
using LT.Contracts.Constants;
using LT.Contracts.DtoMappings.ChartTemplates;
using LT.Contracts.Dtos.ChartTemplates;
using LT.Contracts.Dtos.Indicators;
using LT.Contracts.Entities;
using LT.Contracts.Enums;
using LT.Contracts.Enums.ValidationCodes;

namespace LT.Contracts.Helpers;

public static class ChartTemplateHelpers
{
	// Exported files: camelCase, timeframes by name. Reading takes either case and numbers for
	// enums too, skips comments and trailing commas (hand-edited files) and ignores unknown fields
	private static readonly JsonSerializerOptions FileJsonOptions = new()
	{
		WriteIndented = true,
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		PropertyNameCaseInsensitive = true,
		ReadCommentHandling = JsonCommentHandling.Skip,
		AllowTrailingCommas = true,
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
		Converters = { new JsonStringEnumConverter() },
	};

	/// <summary>The saved templates (an older settings file has none; null entries are dropped).</summary>
	public static List<ChartTemplateEntity> GetTemplates(SettingsEntity settings)
	{
		settings.ChartTemplates ??= [];
		settings.ChartTemplates.RemoveAll(x => x == null);
		return settings.ChartTemplates;
	}

	/// <summary>
	/// The template as shown; <paramref name="defaultId"/> marks the default one. A template
	/// written before periods that holds a timeframe holds <see cref="ChartPeriod.Auto"/> with it
	/// (what charts showed then), so charts following it share their period too. One written
	/// before volume was an indicator that holds indicators holds the volume indicator too
	/// (charts always drew the volume bars then).
	/// </summary>
	public static ChartTemplateDto ToDto(ChartTemplateEntity template, string? defaultId)
	{
		var dto = new ChartTemplateDtoMapper().ToMapped(template);
		dto.IsDefault = defaultId != null && template.Id == defaultId;
		if (
			template.Version < ChartTemplateConstants.PeriodSinceVersion
			&& dto.Settings.Timeframe != null
		)
			dto.Settings.Period ??= ChartPeriod.Auto;
		if (
			template.Version < ChartTemplateConstants.VolumeIndicatorSinceVersion
			&& dto.Settings.Indicators is { } indicators
		)
			dto.Settings.Indicators = IndicatorHelpers.AddVolume(indicators);
		return dto;
	}

	/// <summary>A template to store, written in the <see cref="ChartTemplateConstants.CurrentVersion"/> format.</summary>
	public static ChartTemplateEntity ToEntity(
		string id,
		string name,
		ChartTemplateSettingsDto settings
	) =>
		new ChartTemplateEntityMapper().ToMapped(
			new ChartTemplateDto
			{
				Id = id,
				Name = name,
				Version = ChartTemplateConstants.CurrentVersion,
				Settings = settings,
			}
		);

	public static ChartTemplateEntity? FindByName(
		IEnumerable<ChartTemplateEntity> templates,
		string name
	) =>
		templates.FirstOrDefault(x =>
			string.Equals(x.Name, name.Trim(), StringComparison.OrdinalIgnoreCase)
		);

	/// <summary>
	/// <paramref name="name"/> when no template has it, else the first free "name (2)", "name (3)"
	/// …, the name shortened so the result stays within <see cref="ChartTemplateConstants.NameMaxLength"/>.
	/// </summary>
	public static string GetFreeName(string name, IEnumerable<string> existing)
	{
		var names = existing.ToHashSet(StringComparer.OrdinalIgnoreCase);
		name = name.Trim();
		if (!names.Contains(name))
			return name;
		for (var n = 2; ; n++)
		{
			var suffix = $" ({n})";
			var stem =
				name.Length + suffix.Length > ChartTemplateConstants.NameMaxLength
					? name[..(ChartTemplateConstants.NameMaxLength - suffix.Length)].TrimEnd()
					: name;
			var candidate = stem + suffix;
			if (!names.Contains(candidate))
				return candidate;
		}
	}

	/// <summary>The validation message for a template name; null when it is fine.</summary>
	public static string? GetNameError(string? name) =>
		string.IsNullOrWhiteSpace(name)
			? ChartTemplateValidationCodes.CHTVC_001.GetValidationMessage()
		: name.Trim().Length > ChartTemplateConstants.NameMaxLength
			? ChartTemplateValidationCodes.CHTVC_002.GetValidationMessage(
				ChartTemplateConstants.NameMaxLength
			)
		: null;

	/// <summary>
	/// The validation message for a template's settings (<paramref name="label"/> names the
	/// template in it); null when they are fine.
	/// </summary>
	public static string? GetSettingsError(string label, ChartTemplateSettingsDto settings)
	{
		if (settings.Timeframe is { } timeframe && !Enum.IsDefined(timeframe))
			return ChartTemplateValidationCodes.CHTVC_008.GetValidationMessage(label);
		if (settings.Period is { } period && !Enum.IsDefined(period))
			return ChartTemplateValidationCodes.CHTVC_014.GetValidationMessage(label);
		if (settings.RightMargin is { } margin && !(double.IsFinite(margin) && margin >= 0))
			return ChartTemplateValidationCodes.CHTVC_012.GetValidationMessage(label);
		if (settings.Indicators is not { } indicators)
			return null;
		if (indicators.Count > IndicatorConstants.MaxIndicatorsPerChart)
			return ChartTemplateValidationCodes.CHTVC_009.GetValidationMessage(
				label,
				IndicatorConstants.MaxIndicatorsPerChart
			);
		foreach (var indicator in indicators)
		{
			if (string.IsNullOrWhiteSpace(indicator.DefinitionId))
				return ChartTemplateValidationCodes.CHTVC_010.GetValidationMessage(label);
			foreach (var (name, value) in indicator.Inputs)
				if (!double.IsFinite(value))
					return ChartTemplateValidationCodes.CHTVC_011.GetValidationMessage(label, name);
		}
		return null;
	}

	/// <summary>
	/// A template's indicators for one chart: copies with new instance ids (two charts, or the
	/// same template applied twice, never share an instance).
	/// </summary>
	public static List<IndicatorInstanceDto> CopyIndicators(
		IEnumerable<IndicatorInstanceDto> indicators
	) =>
		[
			.. indicators
				.Take(IndicatorConstants.MaxIndicatorsPerChart)
				.Select(x => new IndicatorInstanceDto
				{
					DefinitionId = x.DefinitionId,
					Inputs = new Dictionary<string, double>(x.Inputs),
					IsVisible = x.IsVisible,
					PlotColors = x.PlotColors is { } colors
						? new Dictionary<string, uint>(colors)
						: null,
				}),
		];

	/// <summary>
	/// The chart's setup (<paramref name="chart"/>, every setting held) already looks as the
	/// template sets it: every setting the template holds is equal; indicators compare by type,
	/// inputs, visibility and colours in legend order, not by instance id.
	/// </summary>
	public static bool Matches(ChartTemplateSettingsDto template, ChartTemplateSettingsDto chart)
	{
		if (template.Timeframe is { } timeframe && chart.Timeframe != timeframe)
			return false;
		if (template.Period is { } period && chart.Period != period)
			return false;
		if (template.RightMargin is { } margin && chart.RightMargin != Math.Max(0, margin))
			return false;
		if (template.Indicators is not { } indicators)
			return true;
		var own = chart.Indicators ?? [];
		var expected = indicators.Take(IndicatorConstants.MaxIndicatorsPerChart).ToList();
		return own.Count == expected.Count
			&& own.Zip(expected).All(x => IndicatorMatches(x.First, x.Second));
	}

	/// <summary>
	/// What saving <paramref name="chart"/>'s setup to the template stores: the chart's value of
	/// every setting the template holds; settings it doesn't hold stay out of it.
	/// </summary>
	public static ChartTemplateSettingsDto Merge(
		ChartTemplateSettingsDto template,
		ChartTemplateSettingsDto chart
	) =>
		new()
		{
			Timeframe = template.Timeframe != null ? chart.Timeframe : null,
			Period = template.Period != null ? chart.Period : null,
			RightMargin = template.RightMargin != null ? chart.RightMargin ?? 0 : null,
			Indicators = template.Indicators != null ? [.. chart.Indicators ?? []] : null,
		};

	private static bool IndicatorMatches(IndicatorInstanceDto a, IndicatorInstanceDto b) =>
		a.DefinitionId == b.DefinitionId
		&& a.IsVisible == b.IsVisible
		&& DictionaryEquals(a.Inputs, b.Inputs)
		&& DictionaryEquals(a.PlotColors ?? [], b.PlotColors ?? []);

	private static bool DictionaryEquals<T>(
		IReadOnlyDictionary<string, T> a,
		IReadOnlyDictionary<string, T> b
	) =>
		a.Count == b.Count
		&& a.All(x =>
			b.TryGetValue(x.Key, out var value)
			&& EqualityComparer<T>.Default.Equals(x.Value, value)
		);

	public static string SerializeFile(IEnumerable<ChartTemplateEntity> templates) =>
		JsonSerializer.Serialize(
			new ChartTemplateFileEntity
			{
				Format = ChartTemplateConstants.FileFormat,
				Version = ChartTemplateConstants.CurrentVersion,
				Templates = [.. templates],
			},
			FileJsonOptions
		);

	/// <summary>The file's content; null when it isn't JSON of that shape.</summary>
	public static ChartTemplateFileEntity? ParseFile(string json)
	{
		try
		{
			return JsonSerializer.Deserialize<ChartTemplateFileEntity>(json, FileJsonOptions);
		}
		catch (JsonException)
		{
			return null;
		}
	}

	/// <summary>A file name for exporting <paramref name="templateName"/>, without the characters file systems refuse.</summary>
	public static string GetFileName(string templateName)
	{
		var invalid = Path.GetInvalidFileNameChars()
			.Concat(['/', '\\', ':', '*', '?', '"', '<', '>', '|'])
			.ToHashSet();
		var name = new string([.. templateName.Trim().Select(x => invalid.Contains(x) ? '_' : x)]);
		return (name.Length > 0 ? name : ChartTemplateConstants.AllTemplatesFileName)
			+ ChartTemplateConstants.FileExtension;
	}
}
