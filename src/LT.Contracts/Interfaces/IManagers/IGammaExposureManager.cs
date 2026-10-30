using LT.Contracts.Dtos.GammaExposure;
using LT.Contracts.Requests.GammaExposure;

namespace LT.Contracts.Interfaces.IManagers;

public interface IGammaExposureManager
{
	/// <summary>
	/// Reads one expiration's gamma exposure by strike: the strikes around the underlying's price,
	/// each call and put streamed (generic tick 101) through <see cref="IMarketDataManager"/> just
	/// until IB's model gamma and its open interest came, at most
	/// <c>GammaExposureConstants.MaxConcurrentLines</c> at a time and never into the last
	/// <c>GammaExposureConstants.ReservedLines</c> free lines. <paramref name="progress"/> hears
	/// each option done.
	/// </summary>
	Task<GammaExposureDto> GetGammaExposureAsync(
		GetGammaExposureRequest request,
		IProgress<GammaExposureProgressDto>? progress = null,
		CancellationToken cancellationToken = default
	);
}
