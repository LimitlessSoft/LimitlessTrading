using System.Globalization;
using LT.App.Helpers;
using LT.Contracts.Dtos.Performance;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;

namespace LT.App.Widgets.DayTradePerformance;

/// <summary>One closed trade in the table, formatted once; times in <c>zone</c> (the display zone).</summary>
public class TradeRowViewModel(RoundTripDto trade, TimeZoneInfo zone)
{
	public string Symbol { get; } = trade.Symbol;
	public string Account { get; } = trade.Account;
	public string Side { get; } = trade.Side == TradeSide.Long ? "Long" : "Short";
	public string Quantity { get; } = FormatHelpers.Quantity(trade.Quantity);
	public string Entry { get; } = FormatHelpers.Price(trade.EntryPrice);
	public string Exit { get; } = FormatHelpers.Price(trade.ExitPrice);
	public string Opened { get; } = FormatTime(trade.OpenTime, zone);
	public string Closed { get; } = FormatTime(trade.CloseTime, zone);
	public string Hold { get; } = FormatHold(trade.HoldTime);
	public string Commission { get; } = FormatHelpers.Money(trade.Commission);
	public string NetPnl { get; } = FormatHelpers.SignedMoney(trade.NetPnl);
	public bool IsWin { get; } = trade.NetPnl > 0;
	public bool IsLoss { get; } = trade.NetPnl < 0;

	private static string FormatTime(DateTimeOffset? time, TimeZoneInfo zone) =>
		time is { } t
			? DisplayTimeHelpers.ToZone(t, zone).ToString("HH:mm:ss", CultureInfo.CurrentCulture)
			: FormatHelpers.Empty;

	/// <summary>"45s", "12m 05s", "3h 20m", "2d 4h".</summary>
	public static string FormatHold(TimeSpan? hold) =>
		hold switch
		{
			null => FormatHelpers.Empty,
			{ TotalDays: >= 1 } h => $"{(int)h.TotalDays}d {h.Hours}h",
			{ TotalHours: >= 1 } h => $"{(int)h.TotalHours}h {h.Minutes:00}m",
			{ TotalMinutes: >= 1 } h => $"{(int)h.TotalMinutes}m {h.Seconds:00}s",
			{ } h => $"{Math.Max(0, (int)h.TotalSeconds)}s",
		};
}
