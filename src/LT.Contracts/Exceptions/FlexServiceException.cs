namespace LT.Contracts.Exceptions;

/// <summary>
/// The Flex Web Service failed: <see cref="ErrorCode"/> is IB's code (null for network / format
/// problems), the message is ready to show and never contains the token.
/// </summary>
public class FlexServiceException(string message, int? errorCode = null, Exception? inner = null)
	: Exception(message, inner)
{
	public int? ErrorCode { get; } = errorCode;
}
