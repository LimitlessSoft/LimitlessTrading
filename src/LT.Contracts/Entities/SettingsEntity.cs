namespace LT.Contracts.Entities;

public class SettingsEntity
{
	public ConnectionSettingsEntity Connection { get; set; } = new();
	public GatewaySettingsEntity Gateway { get; set; } = new();
	public GeneralSettingsEntity General { get; set; } = new();
	public LayoutEntity Layout { get; set; } = new();
	public List<ContractEntity> Watchlist { get; set; } = [];

	/// <summary>Contracts picked by ticker (newest first), offered in the chart's ticker box.</summary>
	public List<ContractEntity> RecentContracts { get; set; } = [];
	public string? SelectedAccount { get; set; }
}
