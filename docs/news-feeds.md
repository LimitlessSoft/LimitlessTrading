# Our own news source (LimitlessTrading feeds)

The News widget shows IB's providers while connected and, always, the app's own source: public
feeds the app fetches itself over HTTP and keeps in a local archive. No server, no account, no IB
connection. Plan and review: [plans/own-news-source.md](../plans/own-news-source.md).

```
NewsViewModel ──► INewsManager (merges, dedupes, archives) ──┬─► IbkrNewsSource ──► IIbkrApiClient
                                                             ├─► FeedNewsSource ──► INewsFeedPoller ──► IFeedClient (LT.Feeds)
                                                             │                                     └──► ISecApiClient (ticker ↔ CIK)
                                                             └─► INewsArchiveManager ──► INewsArchiveRepository (app-data/news/)
```

## Feeds

`FeedNewsConstants.Feeds` is the catalog (code = chip label, name, kind, broad URLs, symbol URL,
poll interval, on by default). Every URL answered a plain GET with the contact User-Agent
(`LimitlessTrading/<version> office@limitlesssoft.com`, which the SEC requires) on 2026-10-29.

| Chip | Feed | Broad | Per symbol | Poll | Default | Notes |
|---|---|---|---|---|---|---|
| SEC | EDGAR Atom: `getcurrent&type=8-K` (broad), `getcompany&CIK=…&dateb=…` (symbol) | yes | yes (CIK) | 30 s | on | Headline built from the title and items: "Shutterstock, Inc. (SSTK) 8-K: Officers / directors, Reg FD". Filers tagged with their tickers by CIK. Company feed pages back with `dateb`. ISO-8859-1. |
| PRN | `prnewswire.com/rss/news-releases-list.rss` | yes | via tags | 60 s | on | Tickers parsed from `(NYSE: BAC)`, `(NASDAQ: KMB)`. |
| FED | `federalreserve.gov/feeds/press_all.xml` | yes | — | 60 s | on | FOMC statements land at 2:00 pm ET, so polled every minute (plan said 10 min). |
| BLS | `bls.gov/feed/{empsit,cpi,ppi,jolts,eci,realer,ximpim}.rss` (Atom) | yes | — | 2 min | on | One feed per release; `bls_latest.rss` is a single "latest numbers" item and not used. |
| BW | Business Wire earnings channel `feed.businesswire.com/rss/home/?rss=G1QFDERJXkJeEF9YXA==` | yes | via tags | 2 min | off | The "home" channel from the plan is deactivated; the earnings channel works. Some items are translations (es, de …). Dates in zone `UT`. |
| FDA | FDA press releases RSS | yes | — | 10 min | off | Slow feed; zone names `EDT`. |
| CNBC | `search.cnbc.com/rs/search/combinedcms/view.xml?partnerId=wrss01&id=100003114` | yes | — | 2 min | off | Personal-use RSS terms. |
| MW | `feeds.content.dowjones.io/public/rss/mw_topstories` | yes | — | 2 min | off | Personal-use RSS terms. |
| YF | `feeds.finance.yahoo.com/rss/2.0/headline?s=<SYM>` | — | yes | 2 min | off | Unofficial; best free per-symbol feed. |
| SA | `seekingalpha.com/api/sa/combined/<SYM>.xml` | — | yes | 5 min | off | Unofficial; personal, non-commercial use. Items carry `sa:stock` tickers. |

Left out:

- **GlobeNewswire** — every `globenewswire.com/RssFeed/…` URL answers 403 (Akamai) to a plain
  client. Not worked around.
- **Google News** — query search, noisy, and its terms forbid automated use.
- **Nasdaq news** (`api.nasdaq.com/api/news/topic/articlebysymbol`) — with the browser headers
  `NasdaqApiClient` sends it gave no answer within 20 s (the calendar endpoints answer). Not
  pursued further (no bot-check circumvention).
- **ACCESS Newswire** — no feed (404).

Unofficial endpoints can change or disappear; a failing feed is that provider's problem only: the
poller logs it once to Messages ("News feeds"), retries at its interval and logs again when it
answers. Terms: we show headline, summary and link only and never scrape a story page; the user's
own machine fetches and keeps its own copy. Revisit if the app is ever sold.

## Fetching (`LT.Feeds`, `NewsFeedPoller`)

- `FeedClient` (`IFeedClient`): one `HttpClient` (gzip, 20 s timeout), one request at a time per
  host and 250 ms apart (well within the SEC's 10 a second), conditional GET (`If-None-Match` /
  `If-Modified-Since`): an unchanged feed is a 304. `FeedParser` reads RSS 2.0 and Atom with
  `XDocument` (encoding from the XML declaration), dates RFC 822 with offsets or zone names
  (GMT, UT, EST/EDT …) and ISO 8601. Not a feed (an HTML error page) is a `FeedServiceException`.
- `NewsFeedPoller` (singleton): one loop per feed URL, ref-counted by watchers — three widgets on
  SPY cost one request per interval — on `TimeProvider`. Every polled item is archived; watchers
  get only the ones the archive hadn't seen. `FetchAsync` reads a symbol's feed once per 5 min
  (`SymbolFetchTtl`) for history. The SEC ticker file (`ISecApiClient.GetTickerCiksAsync`) is read
  once per run for CIK ↔ ticker.
- **Background collection** (`NewsFeedCollector`, hosted): the default-on broad feeds (SEC, PRN,
  FED, BLS) are polled the whole time the app runs, with or without a News widget, and stop with
  the Settings switch. Other feeds are polled only while a widget has them selected; symbol feeds
  only while a widget shows that symbol.
- `FeedNewsSource` (`INewsSource`, id `lt`, "LimitlessTrading"): available while
  `AppSettingsDto.NewsFeeds` is on. Live: a symbol's feeds plus the tagging broad feeds (SEC,
  PRN, BW) filtered to the symbol; All news: the broad feeds. History: the archive (merged by the
  manager) plus the symbol feeds fetched once; SEC's company feed pages back with "Load older".
  Articles: the feed summary with its link ("Open in browser"); a SEC filing reads its index page
  (form, dates, items, documents) and its EX-99.1 press release when it has one.

## Per-provider capabilities

`NewsProviderDto.Capabilities` (IB: everything; ours per feed) and `SelectedByDefault`. The
manager hands a source only the providers that fit the request (BySymbol for Linked, BroadTape for
All news); the widget dims chips that have nothing to give in the current mode. A widget's saved
selection (`WidgetPlacementDto.NewsProviders`) holds a `source:` marker per source it saved, so a
layout saved before the source existed gets its defaults, and "none of this source" survives.

## Local archive

`news/<yyyy-MM-dd>.jsonl` (UTC day) in the app-data folder, one `NewsHeadlineEntity` per line,
append-only (a crash loses at most the line being written; a broken line is skipped and the next
append starts a new line). Articles read once go to `news/articles/<yyyy-MM-dd>.jsonl` (documents
are not kept), so reopening a headline works offline.

- **Everything** passing through `NewsManager` is archived, IB's headlines included; the
  `NewsArchiveManager` keeps the keys of every day it touched (each headline once), the newest 3
  days in memory, and per day read the symbols / providers in it so a symbol's history skips days
  without it.
- History = available sources + archive, merged newest first, deduped by key and by story.
- **Retention**: `NewsRetentionDays` (default 365, 0 = forever); whole days past it are deleted on
  start and when the setting changes. Settings › News › Archive shows the size and "Clear news
  archive".
- Size: a busy day of the default feeds is a few thousand lines, well under 2 MB.

## Dedupe, alerts, search

- **Same story** from two providers (a press release on PRN and on IB's Dow Jones): the headline's
  words (letters and digits, lower case) within `NewsConstants.StoryWindow` (10 min) — the first
  published stays. One provider repeating a headline is never a repeat (SEC "Form 4" filings).
- **Alerts** (Settings › News › Alerts): keywords (comma-separated, whole words, headline or
  summary) and "headlines about a watchlist symbol". `INewsAlertManager` marks matches in the
  widget (accent bar, bold); the widget's "Matches" toggle shows only them (saved per widget,
  `NewsOnlyMatches`); with "Play a sound" on, a new match plays the news chime
  (`ISoundPlayer.PlayNews`, two falling tones) — once per batch, not for headlines older than
  15 min (a feed's backlog on start). It sees everything archived for the first time:
  background feeds always, IB headlines while a News widget streams them.
- **Search** (the widget's Search mode): every word in the headline, summary, provider or tickers,
  over the whole archive and every source (IB's archived headlines too, also while disconnected),
  newest first, "Load older" pages back.

## Not done

- Tagged broad items get a symbol, not a conId: clicking a headline doesn't move the link group.
- IB's chips disappear while disconnected, so Linked / All news show only our feeds then; IB's
  archived headlines are reachable through Search.
