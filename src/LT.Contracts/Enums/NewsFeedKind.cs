namespace LT.Contracts.Enums;

/// <summary>How the app reads one of its own news feeds (<c>FeedNewsConstants.Feeds</c>).</summary>
public enum NewsFeedKind
{
	/// <summary>Plain RSS 2.0 or Atom: title, time, link, summary.</summary>
	Rss = 0,

	/// <summary>SEC EDGAR's Atom: a filing per entry, tagged with the filer's ticker by its CIK.</summary>
	Sec = 1,
}
