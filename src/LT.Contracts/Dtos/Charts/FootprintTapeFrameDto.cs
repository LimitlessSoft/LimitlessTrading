namespace LT.Contracts.Dtos.Charts;

/// <summary>
/// One frame of a footprint trade cache file (<c>FootprintTapeHelpers</c>): the trades of
/// <see cref="From"/> to before <see cref="To"/>, its payload at <see cref="PayloadOffset"/> in
/// the file, the next frame at <see cref="End"/>.
/// </summary>
public readonly record struct FootprintTapeFrameDto(
	DateTimeOffset From,
	DateTimeOffset To,
	int Count,
	int PayloadOffset,
	int PayloadLength,
	int End
);
