using System.Text.Json;
using LT.Contracts.Constants;
using LT.Contracts.Entities;
using LT.Contracts.Helpers;
using LT.Contracts.Interfaces.IRepositories;

namespace LT.Repository.Repositories;

/// <summary>
/// The patch notes' cache and the last version run, as JSON in the app-data folder
/// (patch-notes.json). Registered as a singleton.
/// </summary>
public class PatchNotesRepository(string path) : IPatchNotesRepository
{
	private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
	private readonly Lock _lock = new();

	public PatchNotesRepository()
		: this(Path.Combine(AppPathHelpers.GetAppDataDirectory(), AppConstants.PatchNotesFileName))
	{ }

	public PatchNotesStateEntity Get()
	{
		lock (_lock)
		{
			try
			{
				if (!File.Exists(path))
					return new PatchNotesStateEntity();
				return JsonSerializer.Deserialize<PatchNotesStateEntity>(
						File.ReadAllText(path),
						JsonOptions
					) ?? new PatchNotesStateEntity();
			}
			catch (Exception ex) when (ex is JsonException or IOException)
			{
				// Unreadable: fetched again; at worst one start doesn't offer the new version's notes
				return new PatchNotesStateEntity();
			}
		}
	}

	public void Save(PatchNotesStateEntity state)
	{
		lock (_lock)
		{
			Directory.CreateDirectory(Path.GetDirectoryName(path)!);
			var temp = path + ".tmp";
			File.WriteAllText(temp, JsonSerializer.Serialize(state, JsonOptions));
			File.Move(temp, path, overwrite: true);
		}
	}
}
