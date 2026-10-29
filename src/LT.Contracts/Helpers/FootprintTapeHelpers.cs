using System.Buffers.Binary;
using LT.Contracts.Dtos.Charts;
using LT.Contracts.Enums;

namespace LT.Contracts.Helpers;

/// <summary>
/// The footprint trade cache: its file format, the stretches of time it covers and the gaps
/// between them.
/// <para>
/// A file is an 8-byte header (<c>LTFT</c>, version) and frames appended one after another. A
/// frame is its payload's length and FNV-1a checksum (4 bytes each, little endian) and the
/// payload: the stretch it covers (from, Unix seconds, and its length), the trade count, the
/// decimals prices and sizes are stored with, then each trade as varints — the seconds since the
/// one before shifted left two with the quote side in the low bits, the price change (zigzag) and
/// the size, both scaled to whole numbers. SPY trades take ~4 bytes each. A stretch is marked
/// covered only by the frame that holds its trades, so a torn or corrupt frame (a crash
/// mid-write) is dropped whole together with everything after it.
/// </para>
/// </summary>
public static class FootprintTapeHelpers
{
	/// <summary>Bytes of the file header.</summary>
	public const int FileHeaderLength = 8;

	private const int FrameHeaderLength = 8;
	private const byte Version = 1;
	private const int MaxDecimals = 8;

	// A frame longer than this is corrupt (1000 trades are ~4 KB; a day's live recording is many)
	private const int MaxPayloadLength = 64 * 1024 * 1024;

	private static readonly byte[] Magic = "LTFT"u8.ToArray();

	private static readonly double[] Powers =
	[
		1,
		10,
		100,
		1_000,
		10_000,
		100_000,
		1_000_000,
		10_000_000,
		100_000_000,
	];

	public static byte[] CreateFileHeader()
	{
		var header = new byte[FileHeaderLength];
		Magic.CopyTo(header, 0);
		header[4] = Version;
		return header;
	}

	public static bool IsFileHeader(ReadOnlySpan<byte> data) =>
		data.Length >= FileHeaderLength && data[..4].SequenceEqual(Magic) && data[4] == Version;

	/// <summary>
	/// A frame holding the trades from <paramref name="start"/> to before <paramref name="end"/>
	/// of <paramref name="trades"/> (oldest first, all from <paramref name="from"/> to before
	/// <paramref name="to"/>) as every trade of that stretch.
	/// </summary>
	public static byte[] EncodeFrame(
		DateTimeOffset from,
		DateTimeOffset to,
		IReadOnlyList<FootprintTapeTradeDto> trades,
		int start,
		int end
	)
	{
		var priceDecimals = 0;
		var sizeDecimals = 0;
		for (var i = start; i < end; i++)
		{
			priceDecimals = Math.Max(priceDecimals, GetDecimals(trades[i].Price, priceDecimals));
			sizeDecimals = Math.Max(sizeDecimals, GetDecimals(trades[i].Size, sizeDecimals));
		}
		var payload = new List<byte>(16 + (end - start) * 5);
		var fromSeconds = from.ToUnixTimeSeconds();
		WriteVarint(payload, (ulong)fromSeconds);
		WriteVarint(payload, (ulong)(to.ToUnixTimeSeconds() - fromSeconds));
		WriteVarint(payload, (ulong)(end - start));
		payload.Add((byte)priceDecimals);
		payload.Add((byte)sizeDecimals);
		var time = fromSeconds;
		var price = 0L;
		for (var i = start; i < end; i++)
		{
			var trade = trades[i];
			var side = trade.QuoteSide is FootprintSide.Bid or FootprintSide.Ask
				? (ulong)trade.QuoteSide
				: 0;
			WriteVarint(payload, ((ulong)(trade.UnixSeconds - time) << 2) | side);
			time = trade.UnixSeconds;
			var scaled = (long)Math.Round(trade.Price * Powers[priceDecimals]);
			WriteVarint(payload, ZigZag(scaled - price));
			price = scaled;
			WriteVarint(
				payload,
				(ulong)Math.Max(0, (long)Math.Round(trade.Size * Powers[sizeDecimals]))
			);
		}

		var frame = new byte[FrameHeaderLength + payload.Count];
		payload.CopyTo(frame, FrameHeaderLength);
		BinaryPrimitives.WriteInt32LittleEndian(frame, payload.Count);
		BinaryPrimitives.WriteUInt32LittleEndian(
			frame.AsSpan(4),
			Checksum(frame.AsSpan(FrameHeaderLength))
		);
		return frame;
	}

	/// <summary>
	/// The whole frames of a file (header included), in file order, and where they end
	/// (<paramref name="validLength"/>): a torn or corrupt frame ends the file there. A file
	/// without its header has none (valid length 0).
	/// </summary>
	public static List<FootprintTapeFrameDto> ReadFrames(
		ReadOnlySpan<byte> file,
		out int validLength
	)
	{
		var frames = new List<FootprintTapeFrameDto>();
		validLength = 0;
		if (!IsFileHeader(file))
			return frames;
		var offset = FileHeaderLength;
		validLength = offset;
		while (file.Length - offset >= FrameHeaderLength)
		{
			var length = BinaryPrimitives.ReadInt32LittleEndian(file[offset..]);
			if (length <= 0 || length > MaxPayloadLength || length > file.Length - offset - 8)
				break;
			var payload = file.Slice(offset + FrameHeaderLength, length);
			if (Checksum(payload) != BinaryPrimitives.ReadUInt32LittleEndian(file[(offset + 4)..]))
				break;
			var position = 0;
			if (
				!TryReadVarint(payload, ref position, out var from)
				|| !TryReadVarint(payload, ref position, out var span)
				|| !TryReadVarint(payload, ref position, out var count)
				|| span == 0
				|| count > int.MaxValue
				|| from > (ulong)DateTimeOffset.MaxValue.ToUnixTimeSeconds() - span
			)
				break;
			var end = offset + FrameHeaderLength + length;
			frames.Add(
				new FootprintTapeFrameDto(
					DateTimeOffset.FromUnixTimeSeconds((long)from),
					DateTimeOffset.FromUnixTimeSeconds((long)(from + span)),
					(int)count,
					offset + FrameHeaderLength,
					length,
					end
				)
			);
			offset = end;
			validLength = offset;
		}
		return frames;
	}

	/// <summary>A frame's trades (from <see cref="ReadFrames"/>), added to <paramref name="into"/>.</summary>
	public static void DecodeTrades(
		ReadOnlySpan<byte> file,
		FootprintTapeFrameDto frame,
		List<FootprintTapeTradeDto> into
	)
	{
		var payload = file.Slice(frame.PayloadOffset, frame.PayloadLength);
		var position = 0;
		TryReadVarint(payload, ref position, out var from);
		TryReadVarint(payload, ref position, out _);
		TryReadVarint(payload, ref position, out _);
		if (position + 2 > payload.Length)
			return;
		var priceScale = Powers[Math.Min((int)payload[position++], MaxDecimals)];
		var sizeScale = Powers[Math.Min((int)payload[position++], MaxDecimals)];
		var time = (long)from;
		var price = 0L;
		for (var i = 0; i < frame.Count; i++)
		{
			if (
				!TryReadVarint(payload, ref position, out var timeAndSide)
				|| !TryReadVarint(payload, ref position, out var priceChange)
				|| !TryReadVarint(payload, ref position, out var size)
			)
				return;
			time += (long)(timeAndSide >> 2);
			price += UnZigZag(priceChange);
			into.Add(
				new FootprintTapeTradeDto(
					time,
					price / priceScale,
					size / sizeScale,
					(FootprintSide)(int)(timeAndSide & 3)
				)
			);
		}
	}

	/// <summary>
	/// The parts of <paramref name="from"/> to <paramref name="to"/> that
	/// <paramref name="covered"/> (sorted, apart) leaves out, oldest first.
	/// </summary>
	public static List<(DateTimeOffset From, DateTimeOffset To)> GetGaps(
		IReadOnlyList<(DateTimeOffset From, DateTimeOffset To)> covered,
		DateTimeOffset from,
		DateTimeOffset to
	)
	{
		var gaps = new List<(DateTimeOffset From, DateTimeOffset To)>();
		var cursor = from;
		foreach (var (start, end) in covered)
		{
			if (end <= cursor)
				continue;
			if (start >= to)
				break;
			if (start > cursor)
				gaps.Add((cursor, start));
			cursor = end;
			if (cursor >= to)
				break;
		}
		if (cursor < to)
			gaps.Add((cursor, to));
		return gaps;
	}

	/// <summary>Adds a stretch to <paramref name="covered"/> (sorted, apart), merging what it touches.</summary>
	public static void AddCovered(
		List<(DateTimeOffset From, DateTimeOffset To)> covered,
		DateTimeOffset from,
		DateTimeOffset to
	)
	{
		if (from >= to)
			return;
		var i = 0;
		while (i < covered.Count && covered[i].To < from)
			i++;
		while (i < covered.Count && covered[i].From <= to)
		{
			from = covered[i].From < from ? covered[i].From : from;
			to = covered[i].To > to ? covered[i].To : to;
			covered.RemoveAt(i);
		}
		covered.Insert(i, (from, to));
	}

	/// <summary>
	/// The frames' trades as ranges, oldest first, touching frames merged into one range (frames
	/// never overlap, so their trades just follow each other).
	/// </summary>
	public static List<FootprintTapeRangeDto> ToRanges(
		IEnumerable<(byte[] File, FootprintTapeFrameDto Frame)> frames
	)
	{
		var ranges = new List<FootprintTapeRangeDto>();
		List<FootprintTapeTradeDto>? trades = null;
		foreach (var (file, frame) in frames.OrderBy(x => x.Frame.From))
		{
			var last = ranges.Count > 0 ? ranges[^1] : null;
			if (last == null || frame.From > last.To)
			{
				trades = new List<FootprintTapeTradeDto>(frame.Count);
				last = new FootprintTapeRangeDto
				{
					From = frame.From,
					To = frame.To,
					Trades = trades,
				};
				ranges.Add(last);
			}
			else if (frame.To > last.To)
				last.To = frame.To;
			DecodeTrades(file, frame, trades!);
		}
		return ranges;
	}

	/// <summary>
	/// A range cut into ranges of about <paramref name="maxTrades"/> trades, oldest first, each
	/// cut between two seconds (the trades of a second stay together).
	/// </summary>
	public static IEnumerable<FootprintTapeRangeDto> Slice(
		FootprintTapeRangeDto range,
		int maxTrades
	)
	{
		var trades = range.Trades;
		var start = 0;
		var from = range.From;
		while (trades.Count - start > maxTrades)
		{
			var cut = start + Math.Max(1, maxTrades);
			while (cut < trades.Count && trades[cut].UnixSeconds == trades[cut - 1].UnixSeconds)
				cut++;
			if (cut >= trades.Count)
				break;
			var to = trades[cut].Time;
			yield return new FootprintTapeRangeDto
			{
				From = from,
				To = to,
				Trades = Take(trades, start, cut).ToArray(),
			};
			start = cut;
			from = to;
		}
		yield return new FootprintTapeRangeDto
		{
			From = from,
			To = range.To,
			Trades = start == 0 ? trades : Take(trades, start, trades.Count).ToArray(),
		};
	}

	/// <summary>The first trade at or after <paramref name="unixSeconds"/> (trades oldest first).</summary>
	public static int LowerBound(IReadOnlyList<FootprintTapeTradeDto> trades, long unixSeconds)
	{
		var low = 0;
		var high = trades.Count;
		while (low < high)
		{
			var middle = low + (high - low) / 2;
			if (trades[middle].UnixSeconds < unixSeconds)
				low = middle + 1;
			else
				high = middle;
		}
		return low;
	}

	/// <summary>
	/// Takes the trades before <paramref name="before"/> out of <paramref name="buffer"/> (live
	/// trades waiting to be written), oldest first.
	/// </summary>
	public static List<FootprintTapeTradeDto> TakeBefore(
		List<FootprintTapeTradeDto> buffer,
		DateTimeOffset before
	)
	{
		var seconds = before.ToUnixTimeSeconds();
		var taken = buffer.Where(x => x.UnixSeconds < seconds).OrderBy(x => x.UnixSeconds).ToList();
		buffer.RemoveAll(x => x.UnixSeconds < seconds);
		return taken;
	}

	/// <summary>A live trade as the cache keeps it: its quote side only when the quote rule told it.</summary>
	public static FootprintTapeTradeDto ToTapeTrade(FootprintTradeDto trade) =>
		new(
			trade.Time.ToUnixTimeSeconds(),
			trade.Price,
			(double)trade.Size,
			trade.ByTickRule ? FootprintSide.Unknown : trade.Side
		);

	/// <summary>FNV-1a, 32 bits.</summary>
	public static uint Checksum(ReadOnlySpan<byte> data)
	{
		var hash = 2166136261u;
		foreach (var x in data)
			hash = (hash ^ x) * 16777619u;
		return hash;
	}

	private static IEnumerable<FootprintTapeTradeDto> Take(
		IReadOnlyList<FootprintTapeTradeDto> trades,
		int start,
		int end
	)
	{
		for (var i = start; i < end; i++)
			yield return trades[i];
	}

	// The fewest decimals (at least atLeast) the value is whole with, MaxDecimals at most
	private static int GetDecimals(double value, int atLeast)
	{
		for (var decimals = atLeast; decimals < MaxDecimals; decimals++)
		{
			var scaled = value * Powers[decimals];
			if (Math.Abs(scaled - Math.Round(scaled)) <= 1e-6)
				return decimals;
		}
		return MaxDecimals;
	}

	private static ulong ZigZag(long value) => (ulong)((value << 1) ^ (value >> 63));

	private static long UnZigZag(ulong value) => (long)(value >> 1) ^ -(long)(value & 1);

	private static void WriteVarint(List<byte> into, ulong value)
	{
		while (value >= 0x80)
		{
			into.Add((byte)(value | 0x80));
			value >>= 7;
		}
		into.Add((byte)value);
	}

	private static bool TryReadVarint(ReadOnlySpan<byte> data, ref int position, out ulong value)
	{
		value = 0;
		for (var shift = 0; shift < 64; shift += 7)
		{
			if (position >= data.Length)
				return false;
			var x = data[position++];
			value |= (ulong)(x & 0x7F) << shift;
			if (x < 0x80)
				return true;
		}
		return false;
	}
}
