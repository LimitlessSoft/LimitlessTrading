using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using LT.Contracts.Constants;
using LT.Contracts.Entities;

namespace LT.Contracts.Helpers;

/// <summary>
/// Layout files: the saved layout (<see cref="LayoutEntity"/>) wrapped with a format marker and
/// version, to move it to another computer, keep a copy or share it.
/// </summary>
public static class LayoutFileHelpers
{
	// camelCase on write, any case, comments and trailing commas on read (hand-edited files).
	// Enums stay numbers, as in the settings file: a widget kind this version doesn't know is
	// dropped on load (WorkspaceHelpers.Normalize) instead of failing the whole file.
	private static readonly JsonSerializerOptions FileJsonOptions = new()
	{
		WriteIndented = true,
		Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		PropertyNameCaseInsensitive = true,
		ReadCommentHandling = JsonCommentHandling.Skip,
		AllowTrailingCommas = true,
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
	};

	public static string SerializeFile(LayoutEntity layout) =>
		JsonSerializer.Serialize(
			new LayoutFileEntity
			{
				Format = LayoutFileConstants.FileFormat,
				Version = LayoutFileConstants.CurrentVersion,
				Layout = layout,
			},
			FileJsonOptions
		);

	/// <summary>The file's content; null when it isn't JSON of that shape.</summary>
	public static LayoutFileEntity? ParseFile(string json)
	{
		try
		{
			return JsonSerializer.Deserialize<LayoutFileEntity>(json, FileJsonOptions);
		}
		catch (JsonException)
		{
			return null;
		}
	}
}
