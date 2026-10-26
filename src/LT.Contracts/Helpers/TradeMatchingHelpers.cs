using LT.Contracts.Dtos.Executions;
using LT.Contracts.Dtos.Performance;
using LT.Contracts.Enums;

namespace LT.Contracts.Helpers;

/// <summary>
/// Turns fills into trades. A trade runs from flat to flat in one contract of one account:
/// adding to the position scales in, reducing it scales out, and a fill that goes through zero is
/// split — its closing part ends the trade, the rest opens the next one in the other direction.
/// Realized P&amp;L comes from FIFO lots, commission is split by quantity.
/// </summary>
public static class TradeMatchingHelpers
{
	/// <summary>
	/// Every trade in <paramref name="fills"/> (closed and still open), by close time (open trades
	/// last, by open time). Fills of an unknown side or without quantity are ignored. The fills must
	/// start flat: a sell with no recorded buy before it opens a short.
	/// </summary>
	public static IReadOnlyList<RoundTripDto> Match(IEnumerable<ExecutionDto> fills)
	{
		var trades = new List<RoundTripDto>();
		Run(fills, trades, null);
		return trades
			.OrderBy(x => x.IsClosed ? 0 : 1)
			.ThenBy(x => x.CloseTime ?? x.OpenTime)
			.ThenBy(x => x.OpenTime)
			.ToList();
	}

	/// <summary>
	/// The same matching as <see cref="Match"/>, as the entry → exit pairs of the closed quantity:
	/// each exit fill against the FIFO entry lots it closed, by exit time. Quantity still open has
	/// no leg.
	/// </summary>
	public static IReadOnlyList<TradeLegDto> MatchLegs(IEnumerable<ExecutionDto> fills)
	{
		var legs = new List<TradeLegDto>();
		Run(fills, null, legs);
		return legs.OrderBy(x => x.ExitTime).ThenBy(x => x.EntryTime).ToList();
	}

	private static void Run(
		IEnumerable<ExecutionDto> fills,
		List<RoundTripDto>? trades,
		List<TradeLegDto>? legs
	)
	{
		var groups = fills
			.Where(x => x.Shares != 0 && GetDirection(x.Side) != 0)
			.GroupBy(x => (x.Account, GetContractKey(x)));
		foreach (var group in groups)
		{
			TradeBuilder? open = null;
			var ordered = group
				.OrderBy(x => x.Time ?? DateTimeOffset.MinValue)
				.ThenBy(x => x.ExecId, StringComparer.Ordinal);
			foreach (var fill in ordered)
			{
				var direction = GetDirection(fill.Side);
				var quantity = Math.Abs(fill.Shares);
				var commissionPerUnit = (fill.Commission ?? 0) / (double)quantity;
				var remaining = quantity;
				while (remaining > 0)
				{
					if (open == null || open.Direction == direction)
					{
						open ??= new TradeBuilder(fill, direction, legs);
						open.AddEntry(fill, remaining, commissionPerUnit);
						remaining = 0;
						continue;
					}

					var closing = Math.Min(remaining, open.OpenQuantity);
					open.AddExit(fill, closing, commissionPerUnit);
					remaining -= closing;
					if (open.OpenQuantity == 0)
					{
						trades?.Add(open.Build());
						open = null;
					}
				}
			}
			if (open != null)
				trades?.Add(open.Build());
		}
	}

	/// <summary>+1 for a buy ("BOT", "BUY"), -1 for a sell ("SLD", "SELL"), 0 otherwise.</summary>
	public static int GetDirection(string side) =>
		side.Trim().ToUpperInvariant() switch
		{
			"BOT" or "BUY" or "B" => 1,
			"SLD" or "SELL" or "S" => -1,
			_ => 0,
		};

	/// <summary>The contract a fill belongs to: its conId, else its type and symbol.</summary>
	public static string GetContractKey(ExecutionDto fill) =>
		fill.ConId > 0
			? fill.ConId.ToString(System.Globalization.CultureInfo.InvariantCulture)
			: $"{fill.SecurityType}:{fill.LocalSymbol}:{fill.Symbol}";

	private sealed class TradeBuilder(ExecutionDto first, int direction, List<TradeLegDto>? legs)
	{
		private readonly List<(
			decimal Quantity,
			double Price,
			ExecutionDto Fill,
			double CommissionPerUnit
		)> _lots = [];
		private readonly double _multiplier = first.Multiplier is > 0 ? first.Multiplier.Value : 1;
		private decimal _entryQuantity;
		private double _entryValue;
		private decimal _exitQuantity;
		private double _exitValue;
		private double _gross;
		private double _commission;
		private DateTimeOffset? _closeTime;
		private readonly HashSet<string> _fills = [];

		public int Direction { get; } = direction;

		public decimal OpenQuantity { get; private set; }

		public void AddEntry(ExecutionDto fill, decimal quantity, double commissionPerUnit)
		{
			_lots.Add((quantity, fill.Price, fill, commissionPerUnit));
			OpenQuantity += quantity;
			_entryQuantity += quantity;
			_entryValue += (double)quantity * fill.Price;
			_commission += (double)quantity * commissionPerUnit;
			_fills.Add(fill.ExecId);
		}

		public void AddExit(ExecutionDto fill, decimal quantity, double commissionPerUnit)
		{
			var left = quantity;
			while (left > 0)
			{
				var lot = _lots[0];
				var used = Math.Min(left, lot.Quantity);
				var gross = (fill.Price - lot.Price) * (double)used * _multiplier * Direction;
				_gross += gross;
				left -= used;
				legs?.Add(
					new TradeLegDto
					{
						Account = first.Account,
						Side = Direction > 0 ? TradeSide.Long : TradeSide.Short,
						Quantity = used,
						EntryExecId = lot.Fill.ExecId,
						EntryTime = lot.Fill.Time ?? DateTimeOffset.MinValue,
						EntryPrice = lot.Price,
						ExitExecId = fill.ExecId,
						ExitTime = fill.Time ?? DateTimeOffset.MinValue,
						ExitPrice = fill.Price,
						GrossPnl = gross,
						Commission = (double)used * (lot.CommissionPerUnit + commissionPerUnit),
					}
				);
				// The rest of a partly closed lot stays first in line
				if (used < lot.Quantity)
					_lots[0] = lot with { Quantity = lot.Quantity - used };
				else
					_lots.RemoveAt(0);
			}

			OpenQuantity -= quantity;
			_exitQuantity += quantity;
			_exitValue += (double)quantity * fill.Price;
			_commission += (double)quantity * commissionPerUnit;
			_closeTime = fill.Time;
			_fills.Add(fill.ExecId);
		}

		public RoundTripDto Build() =>
			new()
			{
				Account = first.Account,
				Symbol = string.IsNullOrWhiteSpace(first.LocalSymbol)
					? first.Symbol
					: first.LocalSymbol,
				SecurityType = first.SecurityType,
				Side = Direction > 0 ? TradeSide.Long : TradeSide.Short,
				Quantity = _entryQuantity,
				Multiplier = _multiplier,
				EntryPrice = _entryValue / (double)_entryQuantity,
				ExitPrice = _exitQuantity > 0 ? _exitValue / (double)_exitQuantity : null,
				OpenTime = first.Time ?? DateTimeOffset.MinValue,
				CloseTime = OpenQuantity == 0 ? _closeTime : null,
				IsClosed = OpenQuantity == 0,
				GrossPnl = _gross,
				Commission = _commission,
				FillCount = _fills.Count,
			};
	}
}
