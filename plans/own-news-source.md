# Plan: our own news source

Status: **proposal, for review** (2026-10-29). Nothing implemented yet.

## Goal

The News widget shows headlines from IB only, and only while connected, and only from the API
providers the account has (BRFG, BRFUPDN, DJ-*, DJNL on the live account). Add a second source
that is ours: the app itself collects public feeds (SEC filings, press release wires, macro
releases, per-symbol web news) and shows them next to IB's, with no subscription, no setup and
no IB connection.

The plumbing already expects this: `INewsSource` ("IB now; RSS, Benzinga … later"),
`NewsManager` merges every registered source, provider keys are `source:code` strings, and the
calendar already does the same with Nasdaq's public JSON (`LT.Nasdaq`, `NasdaqCalendarSource`).

## Where the feeds are fetched and kept

**Locally, no server.** The app polls the feeds over HTTP from the user's machine and keeps every
headline it sees in a local archive in the app-data folder. Nothing of ours runs anywhere else:
no hosting, nothing redistributed by us, works without IB, same model as the Nasdaq calendar.
The cost is polling instead of push (30 s – 10 min latency per feed) and that the archive only
holds what this machine saw while the app was running (plus what the feeds still list at start).

## Sources

Probed from here on 2026-10-29 with a plain HTTP GET (read-only):

| Provider (chip) | Feed | By symbol | Broad | Answer | Notes |
|---|---|---|---|---|---|
| **SEC** filings | EDGAR Atom: `browse-edgar?action=getcurrent&type=8-K&output=atom` (broad), `…action=getcompany&CIK=<cik>&output=atom` (symbol) | yes | yes | 200, 40 entries | Primary source, free, no ToS issue. 10 req/s, needs the contact User-Agent — `LT.Sec` already has both and the ticker→CIK map (`ISecApiClient.GetTickerCiksAsync`). Form type + item codes (8-K 2.02 results, 5.02 officers …) make a useful headline. |
| **PRN** PR Newswire | `prnewswire.com/rss/news-releases-list.rss` | via tag | yes | 200, 20 items in 30 min | Company press releases (earnings, M&A) — usually *before* the news sites. Tickers are in the text as `(NYSE: BAC)`, `(Nasdaq: TMDX)`: parse them. |
| **FED** Federal Reserve | `federalreserve.gov/feeds/press_all.xml` | — | yes | 200, 20 items | FOMC statements, rate decisions. |
| **BLS** | `bls.gov/feed/bls_latest.rss` | — | yes | 200 | CPI, jobs, PPI as released; pairs with the calendar's economic events. |
| **FDA** press | `fda.gov/…/press-releases/rss.xml` | via text | yes | 200, 20 items | Approvals; biotech movers. Slow feed (weeks). |
| **CNBC** | `search.cnbc.com/rs/search/combinedcms/view.xml?partnerId=wrss01&id=…` | — | yes | 200, 30 items | Market headlines. Personal-use RSS terms. |
| **MW** MarketWatch | `feeds.content.dowjones.io/public/rss/mw_topstories` | — | yes | 200, 10 items | Same terms as CNBC. |
| **YF** Yahoo Finance | `feeds.finance.yahoo.com/rss/2.0/headline?s=<SYM>` | yes | — | 200, 14 items today | Best free per-symbol feed (aggregates Fool, Zacks, Reuters …). Unofficial endpoint, may vanish. |
| **SA** Seeking Alpha | `seekingalpha.com/api/sa/combined/<SYM>.xml` | yes | — | 200, 30 items over 12 days | Analysis more than news. Unofficial. |
| Google News | `news.google.com/rss/search?q=…` | yes | — | 200, 100 items | **Leave out**: query search, noisy, ToS forbids automated use. |
| GlobeNewswire | `globenewswire.com/RssFeed/…` | — | — | **403** | Blocked plain clients; skip until a working URL is found. |
| Business Wire | `feed.businesswire.com/rss/home/…` | — | — | 200 with "channel unavailable" | Needs a valid channel id; retry in phase 2. |
| ACCESS Newswire | — | — | — | 404 | Skip. |
| Nasdaq news | `api.nasdaq.com/api/news/…` | yes | — | not probed | Needs the browser headers `NasdaqApiClient` sends; check in phase 3 from inside the app. |

Defaults: SEC, PRN, FED, BLS **on**; CNBC, MW, FDA, YF, SA **off** until the user picks them as
chips (they are unofficial or personal-use only). Settings gets one switch for the whole source,
like `CalendarNasdaq`.

Each RSS item gives headline, time, link and a short description — no full article. We show the
description in the reading pane with an **Open in browser** link; we never scrape the page. SEC
is the exception: the filing index is public and can be read as the article (phase 2).

## How it works

```
NewsViewModel ──► INewsManager (merges, dedupes) ──┬─► IbkrNewsSource ──► IIbkrApiClient
                                                   └─► FeedNewsSource ──┬─► INewsArchiveRepository  (LT.Repository: app-data/news/)
                                                                        └─► NewsFeedPoller ──► IFeedClient   (LT.Feeds: HTTP + RSS/Atom)
                                                                                         └─► ISecApiClient (LT.Sec: EDGAR, ticker↔CIK)
```

- **`LT.Feeds`** (new project, refs `LT.Contracts` only, like `LT.Nasdaq`): one `HttpClient`,
  `IFeedClient.GetAsync(url, etag)` → items. RSS 2.0 and Atom parsed with `XDocument` (no
  package); dates in RFC 822 with zone names (`EDT`) and ISO 8601. Conditional GET
  (`ETag` / `If-Modified-Since`) so an unchanged feed costs a 304.
- **`FeedNewsSource : INewsSource`** in `LT.Domain/NewsSources`, id `"lt"`, name
  "LimitlessTrading". `IsAvailable` is always true. Providers come from a static catalog in
  `FeedNewsConstants` (code, name, URL template, by-symbol / broad, poll interval, default on).
- **History** (`GetHeadlinesAsync`) is read from the **local archive** first: newest first,
  paged with `Before` like IB, filtered by symbol tag, so `HasMore` is true as long as older
  archived days exist. When the archive is thin for a symbol (first time it's asked for), the
  symbol feeds (SEC company, YF, SA) are fetched once for its ticker and their items archived;
  SEC's company feed also pages back with `start=` to fill older history on "Load older".
- **Live** (`SubscribeHeadlinesAsync`): polling. A singleton **`NewsFeedPoller`** keeps one timer
  per feed URL, ref-counted by subscribers, so three widgets on SPY cost one request per
  interval. Intervals: SEC current 30 s, PRN 60 s, YF/SA 120 s, FED/BLS/FDA 10 min. New items
  (not seen by key) are archived, then yielded. Uses `TimeProvider` for tests. No market data
  lines.
- **Background collection**: the enabled broad feeds are polled the whole time the app runs,
  with or without a News widget open, so the archive has no gaps while the app is up. Symbol
  feeds are polled only while a widget shows that symbol.

### Local archive

- **`NewsArchiveRepository`** in `LT.Repository` (interface `INewsArchiveRepository` in
  `LT.Contracts`), following the existing repositories: plain files in the app-data folder, no
  database. One append-only JSON-lines file per UTC day, `news/<yyyy-MM-dd>.jsonl`, one
  `NewsHeadlineEntity` per line (source, provider, article id, time, headline, summary, url,
  symbols). Append-only means a crash loses at most the line being written; a bad line is
  skipped on read, not fatal.
- **Everything is stored**, IB headlines included: the manager archives every headline it
  passes on from any source, so IB news seen while connected can be browsed later without a
  connection (IB's own history still answers when connected; archive and IB are merged and
  deduped by key).
- **Index**: on start the last few days are read into memory (key set for dedupe, symbol →
  headlines); older days are read on demand when paging back. A day's file is small (a busy day
  of all default feeds is a few thousand lines, well under 2 MB).
- **Article text** we have (feed summary, SEC filing index, IB article bodies once opened) is
  kept next to it, `news/articles/<yyyy-MM-dd>.jsonl`, so reopening an old headline works offline.
- **Retention**: `AppSettingsDto.NewsRetentionDays` (default 365, 0 = forever); whole day files
  are deleted past it on start. Settings shows the archive size and a "Clear news archive"
  button.
- **Symbol tagging**: broad items get `Symbol` (and, when connected, `ContractId`) from the
  `(Exchange: TICKER)` pattern or the SEC CIK → ticker map, so "Linked" mode can include a
  press release for the linked symbol and a click links the group, as IB headlines do.
- **Keys**: `ArticleId` = the item's guid (or link), so `NewsHelpers.GetKey` dedupes per source.

## Changes to existing code

1. **Widget without IB.** `NewsViewModel` derives from `ConnectedViewModelBase` and loads
   providers only when connected. It must load the providers of every available source, show
   ours while disconnected, and add IB's chips on connect / drop them on disconnect. Linked mode
   needs a symbol, not a conId, when disconnected (`ContractDto` with only `Symbol`).
2. **Per-provider capabilities.** `NewsSourceCapabilities` is per source, but our providers
   differ (CNBC is broad only, YF by symbol only). Add `Capabilities` to `NewsProviderDto` (IB
   sets everything) and have `NewsManager` / the widget check the provider's.
   `HasBroadTapeAsync` keeps working for IB.
3. **Links.** Add `string? Url` to `NewsHeadlineDto` and `NewsArticleDto`; the reading pane shows
   "Open in browser" when set. Interfaces in `LT.Contracts` first (rule 2).
4. **Settings.** `AppSettingsDto.NewsFeeds` (bool, default true) + Settings page switch; the
   source reports unavailable when off. `NewsRetentionDays`, archive size, "Clear news archive".
   `NewsManager` takes `INewsArchiveRepository` (via a `NewsArchive` helper/manager, rule 3) and
   archives what it passes on; it answers history from the archive when no source is available
   instead of failing with NVC_004.
5. **Validation.** `GetNewsHeadlinesRequestValidator` requires a conId today; accept a symbol for
   our source. Messages in `NewsValidationCodes`, added at the end (rule 7).
6. **DI** (`Program.cs`): `IFeedClient` singleton (one HttpClient, User-Agent with contact),
   `INewsArchiveRepository`, `NewsFeedPoller` (started at app start for background
   collection), then `AddSingleton<INewsSource, FeedNewsSource>()` after IB's.
7. **Docs.** New `docs/news-feeds.md` (feeds, limits, terms, what each answered); point
   `vision.md`'s News row and `ibkr-tws-api.md#news` at it.

## Phases

1. **Plumbing + archive + SEC.** `LT.Feeds`, `FeedNewsSource`, poller with background
   collection, local archive (headlines of every source, retention, clear), per-provider
   capabilities, widget works disconnected, `Url` + Open in browser, Settings switch. Providers:
   SEC (by symbol and the 8-K tape). Tests with a fake `IFeedClient`, a temp-folder archive and
   `FakeTimeProvider`; parser tests on saved feed samples.
2. **Wires + macro.** PRN with ticker tagging, FED, BLS, FDA; retry Business Wire /
   GlobeNewswire URLs. SEC filing index as the article text.
3. **Symbol web news.** YF, SA, CNBC, MW (off by default); Nasdaq news if its API answers the
   app's client.
4. **Later.** Cross-source dedupe of the same story (normalised headline + 10 min window, e.g.
   PRN vs IB's DJ), keyword / watchlist filters, sound or alert on a matching headline, full-text
   search over the archive.

## Risks

- **Unofficial endpoints** (YF, SA, CNBC, MW) change or block without notice: a failing feed is
  one provider's error, the rest still show (the manager already does this per source; do it per
  feed inside our source too).
- **Terms**: personal, non-commercial RSS use; we only show headline + summary + link, the
  user's own machine fetches and keeps its own copy. Revisit if the app is ever sold.
- **Polling load**: bounded by the poller (one request per feed URL per interval, symbol feeds
  only for symbols a widget shows). SEC's 10 req/s is enforced by `SecApiClient`.
- **Archive growth**: a few MB a day at most; retention and "Clear" keep it bounded.

## Decisions (review 2026-10-29)

- No server: everything is fetched and stored locally.
- Unofficial feeds (Yahoo, Seeking Alpha, CNBC, MarketWatch) included, off by default.
- Chips name the feeds; the source is "LimitlessTrading" in Settings.
