using System.Text.Json;
using LT.Contracts.Constants;
using LT.Contracts.Entities;
using LT.Contracts.Helpers;
using LT.Contracts.Interfaces.IRepositories;

namespace LT.Repository.Repositories;

/// <summary>
/// The Flex import's progress as JSON in the app-data folder (flex-import.json); no secrets in
/// it (the token is in settings.json). Registered as a singleton.
/// </summary>
public class FlexImportStateRepository : IFlexImportStateRepository
{
	private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
	private readonly Lock _lock = new();
	private readonly string _path = Path.Combine(
		AppPathHelpers.GetAppDataDirectory(),
		FlexConstants.ImportStateFileName
	);

	public FlexImportStateEntity Get()
	{
		lock (_lock)
		{
			if (!File.Exists(_path))
				return new FlexImportStateEntity();
			try
			{
				return JsonSerializer.Deserialize<FlexImportStateEntity>(
						File.ReadAllText(_path),
						JsonOptions
					) ?? new FlexImportStateEntity();
			}
			catch (JsonException)
			{
				// Unreadable: imported again from scratch (Flex merges are idempotent)
				return new FlexImportStateEntity();
			}
		}
	}

	public void Save(FlexImportStateEntity state)
	{
		lock (_lock)
		{
			Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
			var temp = _path + ".tmp";
			File.WriteAllText(temp, JsonSerializer.Serialize(state, JsonOptions));
			File.Move(temp, _path, overwrite: true);
		}
	}
}
