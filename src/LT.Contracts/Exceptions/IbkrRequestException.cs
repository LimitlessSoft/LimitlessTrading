namespace LT.Contracts.Exceptions;

/// <summary>
/// An error IB reported for a request, for callers that act on IB's <see cref="Code"/> (e.g.
/// market depth: 309 too many subscriptions, 354 / 10089 no subscription).
/// </summary>
public class IbkrRequestException(int code, string message, Exception? inner = null)
	: Exception(message, inner)
{
	public int Code { get; } = code;
}
