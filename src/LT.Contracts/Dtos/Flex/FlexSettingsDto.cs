namespace LT.Contracts.Dtos.Flex;

public class FlexSettingsDto
{
	/// <summary>Secret: never logged, masked in the UI.</summary>
	public string Token { get; set; } = string.Empty;
	public string ActivityQueryId { get; set; } = string.Empty;
	public string TradeConfirmQueryId { get; set; } = string.Empty;

	public bool IsConfigured => Token.Length > 0 && ActivityQueryId.Length > 0;
}
