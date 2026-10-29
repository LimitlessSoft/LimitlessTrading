namespace LT.Tests.Feeds;

/// <summary>
/// Real answers of the app's news feeds recorded on 2026-10-29 (Feeds/Fixtures/*.xml), items
/// trimmed to a few; everything else (encoding declarations, namespaces, dates) as sent.
/// </summary>
public static class FeedFixtures
{
	public static Stream Open(string name) =>
		File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Feeds", "Fixtures", name));

	public static string Read(string name) =>
		File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Feeds", "Fixtures", name));
}
