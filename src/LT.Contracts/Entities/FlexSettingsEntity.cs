namespace LT.Contracts.Entities;

/// <summary>
/// The Flex Web Service token and query ids. The token is stored in settings.json in plain text
/// (like the rest of the settings); it only reads reports, it can't trade.
/// </summary>
public class FlexSettingsEntity
{
	public string Token { get; set; } = string.Empty;
	public string ActivityQueryId { get; set; } = string.Empty;
	public string TradeConfirmQueryId { get; set; } = string.Empty;
}
