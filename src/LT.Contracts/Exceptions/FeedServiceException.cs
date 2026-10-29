namespace LT.Contracts.Exceptions;

/// <summary>A news feed request failed (network, HTTP status, not a feed); the message is ready to show.</summary>
public class FeedServiceException(string message, Exception? inner = null)
	: Exception(message, inner);
