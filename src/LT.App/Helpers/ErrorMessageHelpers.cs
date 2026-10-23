using System.Text.Json;
using LSCore.Exceptions;

namespace LT.App.Helpers;

public static class ErrorMessageHelpers
{
	/// <summary>
	/// LSCore validation throws LSCoreBadRequestException whose message is either plain text or a
	/// JSON array of FluentValidation failures; turn both into one line for the UI.
	/// </summary>
	public static string ToUserMessage(Exception exception)
	{
		if (exception is not LSCoreBadRequestException || !exception.Message.StartsWith('['))
			return exception.Message;

		try
		{
			using var document = JsonDocument.Parse(exception.Message);
			return string.Join(
				" ",
				document
					.RootElement.EnumerateArray()
					.Select(x => x.GetProperty("ErrorMessage").GetString())
			);
		}
		catch (JsonException)
		{
			return exception.Message;
		}
	}
}
