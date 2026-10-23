using LT.Contracts.Entities;
using LT.Contracts.Interfaces.IRepositories;

namespace LT.Tests.Fakes;

public class InMemorySettingsRepository : ISettingsRepository
{
	public SettingsEntity Settings { get; private set; } = new();

	public SettingsEntity Get() => Settings;

	public void Save(SettingsEntity settings) => Settings = settings;
}
