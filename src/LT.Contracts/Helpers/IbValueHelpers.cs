namespace LT.Contracts.Helpers;

public static class IbValueHelpers
{
	/// <summary>IB marks "no value" doubles (prices, P&amp;L) with double.MaxValue.</summary>
	public static double? Clean(double? value) =>
		value is null || value == double.MaxValue || !double.IsFinite(value.Value) ? null : value;
}
