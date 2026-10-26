using System.Text.Json;
using LT.Contracts.Constants;
using LT.Contracts.Entities;
using LT.Contracts.Helpers;
using LT.Contracts.Interfaces.IRepositories;

namespace LT.Repository.Repositories;

/// <summary>
/// The price alerts as JSON in the app-data folder (price-alerts.json), apart from settings.json
/// so an alert firing on a price stream never races a settings save. Registered as a singleton.
/// </summary>
public class PriceAlertRepository(string path) : IPriceAlertRepository
{
	private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
	private readonly Lock _lock = new();

	public PriceAlertRepository()
		: this(Path.Combine(AppPathHelpers.GetAppDataDirectory(), PriceAlertConstants.FileName)) { }

	public List<PriceAlertEntity> Get()
	{
		lock (_lock)
		{
			try
			{
				if (!File.Exists(path))
					return [];
				return JsonSerializer
						.Deserialize<PriceAlertFileEntity>(File.ReadAllText(path), JsonOptions)
						?.Alerts.Where(x => x?.Contract != null)
						.ToList()
					?? [];
			}
			catch (Exception ex) when (ex is JsonException or IOException)
			{
				return [];
			}
		}
	}

	public void Save(IReadOnlyList<PriceAlertEntity> alerts)
	{
		lock (_lock)
		{
			Directory.CreateDirectory(Path.GetDirectoryName(path)!);
			var temp = path + ".tmp";
			File.WriteAllText(
				temp,
				JsonSerializer.Serialize(
					new PriceAlertFileEntity { Alerts = [.. alerts] },
					JsonOptions
				)
			);
			File.Move(temp, path, overwrite: true);
		}
	}
}
