using LT.Contracts.Constants;

namespace LT.Contracts.Entities;

public class SettingsEntity
{
	public ConnectionSettingsEntity Connection { get; set; } = new();
	public GatewaySettingsEntity Gateway { get; set; } = new();
	public GeneralSettingsEntity General { get; set; } = new();
	public FlexSettingsEntity Flex { get; set; } = new();
	public LayoutEntity Layout { get; set; } = new();
	public List<WatchlistEntity> Watchlists { get; set; } = [];
	public string? ActiveWatchlistId { get; set; }

	/// <summary>The single watchlist of settings saved before named watchlists; moved into one on load.</summary>
	public List<ContractEntity> Watchlist { get; set; } = [];

	/// <summary>Contracts picked by ticker (newest first), offered in the chart's ticker box.</summary>
	public List<ContractEntity> RecentContracts { get; set; } = [];
	public string? SelectedAccount { get; set; }

	/// <summary>Privacy mode (the eye in the title bar); off in settings saved before it.</summary>
	public bool HideSensitiveValues { get; set; }

	/// <summary>
	/// App-wide UI scale (the status bar picker, Settings → Appearance), percent of the normal
	/// size; 100 in settings saved before it.
	/// </summary>
	public int UiScalePercent { get; set; } = UiScaleConstants.DefaultPercent;

	/// <summary>Named chart setups (timeframe, indicators, …), applied from a chart's header.</summary>
	public List<ChartTemplateEntity> ChartTemplates { get; set; } = [];

	/// <summary>The template applied to every new chart; null: none.</summary>
	public string? DefaultChartTemplateId { get; set; }
}
