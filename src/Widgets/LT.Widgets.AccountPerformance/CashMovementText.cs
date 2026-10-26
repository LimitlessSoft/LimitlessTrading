using System.Globalization;
using LT.Contracts.Dtos.AccountPerformance;
using LT.Contracts.Enums;

namespace LT.App.Widgets.AccountPerformance;

/// <summary>How a deposit, withdrawal or transfer reads in the charts' readout and the calendar's tooltip.</summary>
public static class CashMovementText
{
	public static string Label(CashMovementKind kind) =>
		kind switch
		{
			CashMovementKind.Deposit => "Deposit",
			CashMovementKind.Withdrawal => "Withdrawal",
			CashMovementKind.TransferIn => "Transfer in",
			CashMovementKind.TransferOut => "Transfer out",
			_ => kind.ToString(),
		};

	/// <summary>"Deposit +1,000.00" (base currency).</summary>
	public static string Short(CashMovementDto movement) =>
		$"{Label(movement.Kind)} {Signed(movement.Amount)}";

	/// <summary>
	/// "Deposit +1,000.00 USD (+909.09 EUR) — CASH RECEIPTS": the amount in
	/// <paramref name="baseCurrency"/>, the original one when it differs, and IB's description.
	/// </summary>
	public static string Full(CashMovementDto movement, string baseCurrency)
	{
		var text = $"{Short(movement)} {baseCurrency}".TrimEnd();
		if (
			movement.Currency.Length > 0
			&& !movement.Currency.Equals(baseCurrency, StringComparison.OrdinalIgnoreCase)
		)
			text += $" ({Signed(movement.AmountInCurrency)} {movement.Currency})";
		return movement.Description.Length > 0 ? $"{text} — {movement.Description}" : text;
	}

	private static string Signed(double value) =>
		value.ToString("+#,0.00;-#,0.00;0.00", CultureInfo.CurrentCulture);
}
