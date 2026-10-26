# IB Flex Web Service — account history and trades

The socket API (IB Gateway) has no NAV or P&L history and returns only the current day's
executions, which IBKR.Net can't decode yet (`CVC_007`). So the Account performance and Day trade
performance widgets get their history from IB's **Flex Web Service**: Flex queries the user
creates once in Client Portal, fetched by the app over HTTPS with a token. This is the one place
the app talks to IB outside the Gateway; it was chosen with the user on 2026-10-26 after the
socket-only routes were ruled out (no history at all before the app first ran). Everything after
the one-time setup is automatic.

Code: `IFlexApiClient` (`LT.Ibkr/Flex/FlexApiClient`, `FlexStatementParser`), `IFlexImportManager`
(`LT.Domain/Managers/FlexImportManager`, run by the `FlexImportScheduler` hosted service),
`FlexHelpers` / `FlexImportHelpers` / `AccountPerformanceHelpers.MergeFlexDays` (`LT.Contracts`),
state in `flex-import.json` (`FlexImportStateRepository`).

## Setup (once, by the user)

IB offers no API to create Flex queries or tokens — it can only be done logged in to Client
Portal — so the app guides instead of automating (no browser automation or scraping):

- While Flex isn't configured, Account performance and Day trade performance show a banner at the
  top, "History needs IB Flex — set it up once in Settings.", with a primary **Set up…** that
  opens Settings directly on *Account history → IB Flex* (`SettingsNavigator`). The same banner,
  in the warning style and with the error text, shows for an error only the user can fix:
  expired / invalid token (1012 / 1015), IP restriction (1013), bad query (1014), service off
  (1011), account (1016), unvalidated request (1020), legacy query (1010), or a query missing its
  NAV / Trade Confirmations section (`FlexHelpers.NeedsUserAction`, `FlexStatusHelpers.GetBanner`).
- The Settings page has the steps below as a numbered checklist (`FlexSetupGuide`), each value with
  a **Copy** button, and **Open Flex Queries in Client Portal** on the Client Portal steps. That
  opens `https://www.interactivebrokers.com/sso/resolver?action=RM_FLEX_QUERIES`
  (`FlexConstants.ClientPortalFlexQueriesUrl`) in the default browser through Avalonia's
  `Launcher`: IB's own login resolver, the link IB's Client Portal guide gives as "Log in to Flex
  Queries" (ibkrguides.com, *Flex Queries* and *Enable Flex Web Service* pages, checked
  2026-10-26; it answers a redirect to the regional IB site and the login). IB documents no link
  to the Flex Web Service configuration itself; it is the gear on that same page, which step 3
  says.
- Pasting is forgiving: the token loses any whitespace (`FlexHelpers.NormalizeToken`), and a
  query id field accepts the id, a Flex URL with `q=` / `queryId=`, or text such as "Query ID:
  123456" and keeps the number (`FlexHelpers.ExtractQueryId`) — as soon as it is pasted and again
  on save.

Hand the steps below to the user as is. Labels are Client Portal's as of 2026; IB moves menus now
and then.

1. **Activity Flex Query** — Client Portal → **Performance & Reports → Flex Queries** → in
   *Activity Flex Query* click **+**.
   - Query name: anything, e.g. `LimitlessTrading activity`.
   - **Sections** — click each; a field list pops up with the section's options (checkboxes at
     the top) and its fields: pick the options below, tick **Select All**, Save. Only these four
     are needed; extra sections are ignored. The fields the app reads are listed in brackets.
     - **Account Information** [Account ID, Currency, Date Opened]
     - **Net Asset Value (NAV) in Base** [Account ID, Currency, Report Date, Total]
     - **Cash Transactions** — options: **Deposits & Withdrawals** (others may stay on), level
       **Detail** [Account ID, Currency, FX Rate To Base, Type, Amount, Report Date, Date/Time,
       Description, Level of Detail]
     - **Trades** — options: **Execution** (Closed Lots / Order / summaries may stay on, they
       are skipped) [Account ID, Currency, Asset Class, Symbol, Underlying Symbol, Description,
       Conid, Multiplier, Trade ID, IB Execution ID, Orig Trade ID, Buy/Sell, Quantity, Trade
       Price, IB Commission, IB Commission Currency, Exchange, Date/Time, Trade Date, Realized
       P/L (FIFO), Level of Detail]
   - **Delivery configuration**: Accounts — all you want to see; **Format: XML**; **Period: Last
     365 Calendar Days** (the app always sends its own from / to dates, but IB needs a period).
   - **General configuration**: **Date Format: yyyyMMdd**, **Time Format: HHmmss**,
     **Date/Time Separator: ; (semi-colon)** (IB's defaults); **Display Account Alias in Place
     of Account ID: No** (the ids must match the Gateway's); **Include Offsetting Trade/Cancel
     Pairs?: No**; **Breakout by Day?: No**; Profit and Loss: Default; the other options as they
     are. Then **Continue** and **Create**.
   - **Save**, then note the **Query ID** shown for it in the Activity Flex Query list.
2. **Trade Confirmation Flex Query** (optional — today's trades, a few minutes after each fill) —
   in *Trade Confirmation Flex Query* click **+**: section **Trade Confirmations**, option
   **Executions**, **Select All** fields [Account ID, Trade ID, Exec ID, Conid, Symbol,
   Underlying Symbol, Asset Class, Multiplier, Buy/Sell, Quantity, Price, Commission, Commission
   Currency, Exchange, Date/Time, Trade Date, Level of Detail]; **Format: XML**, **Period:
   Today**, the same date / time format and account-id setting as above. Save, note its Query ID.
3. **Flex Web Service** — **Performance & Reports → Flex Queries → Flex Web Service
   Configuration** (gear icon): switch it **on**, **Generate a New Token** with the longest
   expiry offered and **no IP address** (a token bound to an IP fails from anywhere else, error
   1013). Copy the token. Generating a new token invalidates the old one.
4. In the app: **Settings (Ctrl+,) → Account history → IB Flex**: paste the token and the two
   query IDs, then **Save & import now** (or Save). The first import fetches a year at a time back
   to the account's start; with IB's 10-requests-a-minute limit that takes a few minutes for a
   long history. When the token expires, the status line, the Messages widget and both widgets
   say so (error 1012); generate a new one and paste it.

Formats the parser accepts: dates `yyyyMMdd`, `yyyy-MM-dd`, `MM/dd/yyyy`, `MM/dd/yy`,
`dd-MMM-yy` (`dd/MM/yyyy` is **not** supported — it reads as `MM/dd`); times `HHmmss` or
`HH:mm:ss`; separators `;` `,` space or `T`. Times are taken as US Eastern unless they carry an
offset (`FlexHelpers.ParseDateTime`).

## Web service (v3)

Verified against IB's docs (ibkrguides.com, *Configure Flex Web Service* and *Flex Web Service
Version 3 Error Codes*, 2026-10-26) and a token-less probe of both endpoints (answers 1020).

1. `GET https://ndcdyn.interactivebrokers.com/AccountManagement/FlexWebService/SendRequest?t=<token>&q=<queryId>[&fd=yyyyMMdd&td=yyyyMMdd]&v=3`
   → `<FlexStatementResponse><Status>Success</Status><ReferenceCode>…</ReferenceCode><url>…/GetStatement</url>`.
   `fd` / `td` override the query's period, at most 365 days per request. Without `v=3` IB answers
   in version 2.
2. `GET <url>?t=<token>&q=<ReferenceCode>&v=3` → the `FlexQueryResponse` XML, or a
   `FlexStatementResponse` with `Status Fail` and an `ErrorCode`.
- IB requires a `User-Agent`; the client sends `LimitlessTrading/<version>`.
- **Polling**: first GetStatement after 3 s, then ×1.6 up to 30 s, while IB answers a "try again
  shortly" code (1001, 1004–1009, 1019, 1021); 1018 (too many requests) waits 20 s. One statement
  gives up after 5 minutes (`FlexConstants.StatementTimeout`).
- **Rate limit**: IB allows 1 request per second and 10 per minute per token. Every HTTP call goes
  through one gate and two `RequestThrottle`s: ≥ 1.2 s apart and ≤ 9 per minute. The client is a
  singleton, so the limits hold app-wide. A back-fill year costs 2–4 requests.
- **Errors** become `FlexServiceException` with a message from `FlexValidationCodes`: 1012 token
  expired, 1015 token invalid, 1013 IP restriction, 1014 query invalid, 1018 too many requests,
  1011 service inactive, 1020 couldn't validate, 1003 no statement for the period (ends a
  back-fill), 1010 legacy query; anything else "IB Flex error <code>: <IB's text>".
- **The token is a secret**: only ever in the query string; never in a message or the log (HTTP
  errors carry the status or host, not the URL); the statement URL IB returns is followed only on
  an `https://*.interactivebrokers.com` host, else the default one is used; the Settings field is
  masked. It is stored **in plain text in `settings.json`** (like the rest of the settings), which
  the user accepted — it reads reports and can't trade.

## What is read

`FlexStatementParser` (one `FlexStatement` per account; missing optional attributes stay empty):

| Section (XML element) | Used for |
|---|---|
| `EquitySummaryByReportDateInBase` (`reportDate`, `total`, `currency`, `accountId`) | End-of-day NAV per account, base currency |
| `CashTransaction` with a type containing *Deposit* / *Withdrawal* (detail rows only; `SUMMARY` / `BASE_SUMMARY` rows are skipped) — `amount × fxRateToBase`, `reportDate` (else `dateTime`, `settleDate`) | Net cash flow per day, left out of P&L |
| `Trade` (`levelOfDetail` EXECUTION or none) and `TradeConfirm` — `tradeID`, `ibExecID` / `execID`, `conid`, `symbol`, `underlyingSymbol`, `assetCategory`, `multiplier`, `buySell`, `quantity`, `tradePrice` / `price`, `ibCommission` / `commission` (+ currency, stored positive), `exchange`, `dateTime`, `tradeDate`, `fifoPnlRealized` | Journal fills |
| `buySell` "BUY (Ca.)" / "SELL (Ca.)" with `origTradeID` | Removes the busted original from the journal |
| `AccountInformation` `dateOpened` | Ends the back-fill at the account's start |

A query without the NAV section fails with a message saying what to add (`FVC_018`); a Trade
Confirmation query without its section likewise (`FVC_019`).

## Import schedule

`FlexImportManager`, one run at a time, state saved after every step in `flex-import.json`
(Activity query id, covered from / to, back-fill complete, last success / attempt per query, last
error) so a restart doesn't download again:

1. **First run** (or another Activity query id): the newest year, `today−365 … yesterday` (New
   York dates), so recent history shows first.
2. **Back-fill**: the year before the oldest covered day, repeatedly, until a year comes back
   empty, IB answers 1003, the year contains every account's `dateOpened`, or 30 years. Bounded,
   one year per request, paced by the client.
3. **Daily**: once per New York day, `coveredTo − 7 days … yesterday` (the Activity statement
   covers through the previous business day; the overlap picks up late corrections). After the
   app was off for a long time this becomes several ≤ 365-day requests. A failure retries after an
   hour.
4. **Trade Confirmation**: the query's own period (Today), every 5 minutes
   (`FlexConstants.TradeConfirmInterval`) while the app runs, Gateway connected or not.
5. **On demand**: Settings → *Save & import now* and F5 in either widget run what is due plus the
   Trade Confirmation statement right away (a back-fill in progress is waited for). Saving new Flex
   settings wakes the scheduler and clears the last error.

## Merge rules

**Account history** (`IAccountPerformanceManager.ImportFlexHistory` →
`AccountPerformanceHelpers.MergeFlexDays`):

- A Flex `reportDate` is IB's US trading day and is used as the day key directly — the same New
  York date the recorder buckets live snapshots by.
- Flex is authoritative for days **before today**: net liquidation = NAV `total`, currency = base
  currency, `CashFlow` = the day's net deposits − withdrawals, `Source = Flex`. Realized /
  unrealized P&L recorded live that day are kept. **Today stays live** (Flex rows for today are
  ignored).
- A Flex day's **daily P&L = NAV − previous day's net liquidation − CashFlow** (null for the first
  day with no earlier one). A cash flow on a date without a NAV row moves to the next NAV date.
  The return math then never sees a deposit as a gain: a day's return is P&L over (net
  liquidation − P&L) = previous close + the day's deposits.
- Merges are idempotent; an older year filled in later gives the previous first day its P&L.

**Trade journal** (`ITradeJournalManager.ImportFlexTrades`):

- **Dedupe key: IB's execution id** (`ibExecID` in Activity, `execID` in Trade Confirmation),
  which is the same id the socket API reports as `execId` — so a Flex fill and the same fill from
  `reqExecutions` (once IBKR.Net decodes it) collapse into one. A Flex row without an execution
  id is keyed `flex:<tradeID>`; fills also match by `tradeID`, and a `flex:` key is replaced by the
  real id when one arrives. A later row fills in commission (Activity's is final), realized P&L,
  tradeID, multiplier, conId and local symbol.
- Fills are shaped like socket ones: `Symbol` = underlying, `LocalSymbol` = the listing's code
  (OCC option symbol), side BUY / SELL with a positive quantity, commission positive. Flex's
  `ibOrderID` is not the API order id, so `OrderId` stays 0.
- Days are New York dates of the fill time; a row without a time is put at 16:00 New York on its
  trade date. Round trips and stats then run over the whole imported history.

## Limits and open points

- Times without an offset are taken as US Eastern; IB documents statement times in ET but this is
  not verified against a real statement.
- Today's trades come only from the Trade Confirmation query, typically a few minutes after the
  fill plus up to the 5-minute poll.
- Position transfers (ACATS, internal transfers of positions) are not cash flows; such a day shows
  the transferred value as P&L. Only *Deposits & Withdrawals* cash transactions are left out.
- Amounts are summed as reported per account; journal stats sum across currencies like before.
- Corrections that change an execution id (socket `.01` → `.02` suffix) are not reconciled yet.
- The token expires (at most about a year); the user has to generate a new one. The widgets'
  banner says so.
- IB keeps Flex data for the four previous calendar years and the current one ("Saved Flex Queries
  are available for the four previous calendar years…", Client Portal guide); the back-fill stops
  at the first empty or unavailable year, so older history is not imported.
