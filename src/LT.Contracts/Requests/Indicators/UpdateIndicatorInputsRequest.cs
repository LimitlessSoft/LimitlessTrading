using LT.Contracts.Dtos.Indicators;

namespace LT.Contracts.Requests.Indicators;

/// <summary>New input values for an instance, checked against its definition's inputs.</summary>
public class UpdateIndicatorInputsRequest
{
	public IndicatorDefinitionDto Definition { get; set; } = new();
	public IndicatorInstanceDto Instance { get; set; } = new();
	public Dictionary<string, double> Inputs { get; set; } = [];
}
