namespace LT.Contracts.Entities;

public class SettingsEntity
{
	public ConnectionSettingsEntity Connection { get; set; } = new();
}
