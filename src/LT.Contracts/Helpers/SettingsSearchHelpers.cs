namespace LT.Contracts.Helpers;

public static class SettingsSearchHelpers
{
	/// <summary>
	/// Settings search: every word of <paramref name="query"/> appears in one of
	/// <paramref name="texts"/> (title, description, keywords), in any order and case. An empty
	/// query matches everything.
	/// </summary>
	public static bool Matches(string? query, params string?[] texts)
	{
		var words = (query ?? string.Empty).Split(
			' ',
			StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
		);
		return words.All(word =>
			texts.Any(text => text?.Contains(word, StringComparison.OrdinalIgnoreCase) == true)
		);
	}
}
