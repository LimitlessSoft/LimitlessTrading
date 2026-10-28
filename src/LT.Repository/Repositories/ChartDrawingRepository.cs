using System.Text.Json;
using LT.Contracts.Constants;
using LT.Contracts.Entities;
using LT.Contracts.Helpers;
using LT.Contracts.Interfaces.IRepositories;

namespace LT.Repository.Repositories;

/// <summary>
/// The chart drawings as JSON in the app-data folder (chart-drawings.json), apart from
/// settings.json so a drag on a chart never races a settings save. Registered as a singleton.
/// </summary>
public class ChartDrawingRepository(string path) : IChartDrawingRepository
{
	private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
	private readonly Lock _lock = new();

	public ChartDrawingRepository()
		: this(Path.Combine(AppPathHelpers.GetAppDataDirectory(), ChartDrawingConstants.FileName))
	{ }

	public List<ChartDrawingEntity> Get()
	{
		lock (_lock)
		{
			try
			{
				if (!File.Exists(path))
					return [];
				return JsonSerializer
						.Deserialize<ChartDrawingFileEntity>(File.ReadAllText(path), JsonOptions)
						?.Drawings.Where(x => x?.Points != null)
						.ToList()
					?? [];
			}
			catch (Exception ex) when (ex is JsonException or IOException)
			{
				return [];
			}
		}
	}

	public void Save(IReadOnlyList<ChartDrawingEntity> drawings)
	{
		lock (_lock)
		{
			Directory.CreateDirectory(Path.GetDirectoryName(path)!);
			var temp = path + ".tmp";
			File.WriteAllText(
				temp,
				JsonSerializer.Serialize(
					new ChartDrawingFileEntity { Drawings = [.. drawings] },
					JsonOptions
				)
			);
			File.Move(temp, path, overwrite: true);
		}
	}
}
