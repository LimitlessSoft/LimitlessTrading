using System.Text.Json;
using LT.Contracts.Constants;
using LT.Contracts.Entities;
using LT.Contracts.Helpers;
using LT.Contracts.Interfaces.IRepositories;

namespace LT.Repository.Repositories;

/// <summary>
/// Every note as JSON in the app-data folder (notes.json), written through a temp file so a
/// crash mid-write keeps the previous notes. Registered as a singleton.
/// </summary>
public class NotesRepository : INotesRepository
{
	private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
	private readonly Lock _lock = new();
	private readonly string _path;

	public NotesRepository()
		: this(Path.Combine(AppPathHelpers.GetAppDataDirectory(), NotesConstants.FileName)) { }

	public NotesRepository(string path) => _path = path;

	public NotesEntity Get()
	{
		lock (_lock)
		{
			if (!File.Exists(_path))
				return new NotesEntity();
			try
			{
				return JsonSerializer.Deserialize<NotesEntity>(File.ReadAllText(_path), JsonOptions)
					?? new NotesEntity();
			}
			catch (JsonException)
			{
				// Unreadable: kept aside, so the next save doesn't overwrite what may be recovered
				try
				{
					File.Copy(_path, _path + ".corrupt", overwrite: true);
				}
				catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
				return new NotesEntity();
			}
		}
	}

	public void Save(NotesEntity notes)
	{
		lock (_lock)
		{
			Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
			var temp = _path + ".tmp";
			File.WriteAllText(temp, JsonSerializer.Serialize(notes, JsonOptions));
			File.Move(temp, _path, overwrite: true);
		}
	}
}
