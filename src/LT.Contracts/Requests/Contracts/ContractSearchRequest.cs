namespace LT.Contracts.Requests.Contracts;

public class ContractSearchRequest
{
	public string Symbol { get; set; } = string.Empty;
	public string SecurityType { get; set; } = string.Empty;
	public string? Exchange { get; set; }
	public string? Currency { get; set; }
}
