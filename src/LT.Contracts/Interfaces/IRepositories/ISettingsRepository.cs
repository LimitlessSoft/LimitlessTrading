using LT.Contracts.Entities;

namespace LT.Contracts.Interfaces.IRepositories;

public interface ISettingsRepository
{
	SettingsEntity Get();
	void Save(SettingsEntity settings);
}
