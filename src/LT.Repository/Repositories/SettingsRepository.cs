using System.Text.Json;
using LT.Contracts.Entities;
using LT.Contracts.Helpers;
using LT.Contracts.Interfaces.IRepositories;

namespace LT.Repository.Repositories;

/// <summary>
/// Settings as a JSON file in the OS app-data folder. Registered as a singleton (see Program.cs).
/// </summary>
public class SettingsRepository : ISettingsRepository
{
	private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
	private readonly Lock _lock = new();
	private readonly string _filePath = AppPathHelpers.GetSettingsFilePath();

	public SettingsEntity Get()
	{
		lock (_lock)
		{
			if (!File.Exists(_filePath))
				return new SettingsEntity();

			try
			{
				return JsonSerializer.Deserialize<SettingsEntity>(
						File.ReadAllText(_filePath),
						JsonOptions
					) ?? new SettingsEntity();
			}
			catch (JsonException)
			{
				return new SettingsEntity();
			}
		}
	}

	public void Save(SettingsEntity settings)
	{
		lock (_lock)
		{
			Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
			File.WriteAllText(_filePath, JsonSerializer.Serialize(settings, JsonOptions));
		}
	}
}
