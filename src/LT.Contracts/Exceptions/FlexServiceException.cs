using LT.Contracts.Helpers;

namespace LT.Contracts.Exceptions;

/// <summary>
/// The Flex Web Service failed: <see cref="ErrorCode"/> is IB's code (null for network / format
/// problems), the message is ready to show and never contains the token.
/// <see cref="NeedsUserAction"/>: only the user can fix it (by default from the code, see
/// <see cref="FlexHelpers.NeedsUserAction"/>; a query missing a section says so explicitly).
/// </summary>
public class FlexServiceException(
	string message,
	int? errorCode = null,
	Exception? inner = null,
	bool? needsUserAction = null
) : Exception(message, inner)
{
	public int? ErrorCode { get; } = errorCode;
	public bool NeedsUserAction { get; } =
		needsUserAction ?? FlexHelpers.NeedsUserAction(errorCode);
}
