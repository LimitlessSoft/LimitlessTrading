using System.Text.Json;
using LT.Contracts.Constants;
using LT.Contracts.Entities;
using LT.Contracts.Helpers;
using LT.Contracts.Interfaces.IRepositories;

namespace LT.Repository.Repositories;

/// <summary>
/// The chart drawings as JSON in the app-data folder (chart-drawings.json), apart from
/// settings.json so a drag on a chart never races a settings save. Registered as a singleton.
/// Fields added later (styles, lock / hide, fib levels, tool styles) are simply missing in older
/// files and read as their defaults.
/// </summary>
public class ChartDrawingRepository(string path) : IChartDrawingRepository
{
	private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
	private readonly Lock _lock = new();

	public ChartDrawingRepository()
		: this(Path.Combine(AppPathHelpers.GetAppDataDirectory(), ChartDrawingConstants.FileName))
	{ }

	public ChartDrawingFileEntity Get()
	{
		lock (_lock)
		{
			try
			{
				if (!File.Exists(path))
					return new ChartDrawingFileEntity();
				var file = JsonSerializer.Deserialize<ChartDrawingFileEntity>(
					File.ReadAllText(path),
					JsonOptions
				);
				if (file == null)
					return new ChartDrawingFileEntity();
				// A file written before styles has no tool styles (null when "null" is written)
				return new ChartDrawingFileEntity
				{
					Drawings = [.. (file.Drawings ?? []).Where(x => x?.Points != null)],
					ToolStyles = [.. (file.ToolStyles ?? []).Where(x => x?.Style != null)],
				};
			}
			catch (Exception ex) when (ex is JsonException or IOException)
			{
				return new ChartDrawingFileEntity();
			}
		}
	}

	public void Save(ChartDrawingFileEntity file)
	{
		lock (_lock)
		{
			Directory.CreateDirectory(Path.GetDirectoryName(path)!);
			var temp = path + ".tmp";
			File.WriteAllText(temp, JsonSerializer.Serialize(file, JsonOptions));
			File.Move(temp, path, overwrite: true);
		}
	}
}
