namespace LT.Contracts.Exceptions;

/// <summary>A Yahoo Finance request failed (network, HTTP status, an unreadable page); the message is ready to show.</summary>
public class YahooServiceException(string message, Exception? inner = null)
	: Exception(message, inner);
