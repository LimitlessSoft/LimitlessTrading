namespace LT.Contracts.Exceptions;

/// <summary>A Nasdaq calendar request failed (network, HTTP status, an unreadable answer); the message is ready to show.</summary>
public class NasdaqServiceException(string message, Exception? inner = null)
	: Exception(message, inner);
