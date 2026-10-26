using System.Globalization;

namespace LT.App.Helpers;

public static class FormatHelpers
{
	public const string Empty = "—";

	public static string Price(double? value) =>
		value is { } v
			? v.ToString(Math.Abs(v) < 1 ? "#,0.0000" : "#,0.00", CultureInfo.CurrentCulture)
			: Empty;

	public static string Money(double? value) =>
		value is { } v ? v.ToString("#,0.00", CultureInfo.CurrentCulture) : Empty;

	public static string Money(decimal? value) =>
		value is { } v ? v.ToString("#,0.00", CultureInfo.CurrentCulture) : Empty;

	public static string SignedMoney(double? value) =>
		value is { } v ? v.ToString("+#,0.00;-#,0.00;0.00", CultureInfo.CurrentCulture) : Empty;

	public static string SignedPercent(double? value) =>
		value is { } v
			? v.ToString("+0.00'%';-0.00'%';0.00'%'", CultureInfo.CurrentCulture)
			: Empty;

	public static string Quantity(decimal? value) =>
		value is { } v ? v.ToString("#,0.####", CultureInfo.CurrentCulture) : Empty;

	/// <summary>Compact volume: 950, 12.3K, 4.56M, 1.2B.</summary>
	public static string Volume(decimal? value) =>
		value switch
		{
			null => Empty,
			>= 1_000_000_000 => $"{value / 1_000_000_000:0.##}B",
			>= 1_000_000 => $"{value / 1_000_000:0.##}M",
			>= 10_000 => $"{value / 1_000:0.#}K",
			_ => value.Value.ToString("#,0", CultureInfo.CurrentCulture),
		};

	public static string Time(DateTimeOffset? value) =>
		value is { } v
			? v.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentCulture)
			: Empty;
}
