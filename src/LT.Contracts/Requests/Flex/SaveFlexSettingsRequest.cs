namespace LT.Contracts.Requests.Flex;

/// <summary>All empty turns the Flex import off.</summary>
public class SaveFlexSettingsRequest
{
	public string Token { get; set; } = string.Empty;
	public string ActivityQueryId { get; set; } = string.Empty;
	public string TradeConfirmQueryId { get; set; } = string.Empty;
}
