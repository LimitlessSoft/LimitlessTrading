using LT.Contracts.Dtos.Calendar;

namespace LT.Contracts.Interfaces.IApiClients;

/// <summary>
/// Yahoo Finance's earnings calendar page (server-rendered HTML; no account, no key; unofficial,
/// see docs/architecture.md → External data). Requests are serialized and spaced. Throws
/// <see cref="Exceptions.YahooServiceException"/> with a message ready to show.
/// </summary>
public interface IYahooApiClient
{
	/// <summary>
	/// One page of <paramref name="day"/>'s earnings (New York date), largest companies first,
	/// starting at <paramref name="offset"/> (0-based). Yahoo may ignore the offset: check
	/// <see cref="YahooEarningsPageDto.First"/>.
	/// </summary>
	Task<YahooEarningsPageDto> GetEarningsPageAsync(
		DateOnly day,
		int offset,
		CancellationToken cancellationToken = default
	);
}
