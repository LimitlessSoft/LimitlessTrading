# UI Patterns (Avalonia)

## Stack

- **Avalonia 12** with the Fluent theme, dark variant by default; Inter font.
- **CommunityToolkit.Mvvm** — `[ObservableProperty]` partial properties and `[RelayCommand]`.
- **Compiled bindings** everywhere (`AvaloniaUseCompiledBindingsByDefault`) — every view sets
  `x:DataType`.

## Pages and widgets

Mirrors the Termodom FE split (`pages/` + `widgets/`):

- **Widget** = one self-contained panel: `Widgets/<Name>/<Name>View.axaml` (+ `.axaml.cs`) and
  `<Name>ViewModel.cs`. A widget's ViewModel injects managers and owns its own subscriptions.
- **Page** = a workspace that composes widgets (`Pages/TradingPage`). It doesn't contain trading
  logic, only layout.
- **Shell** (`Shell/MainWindow`) = icon toolbar (for now a single hamburger holding every command), the current page, status bar.
  Connection state sits at the left of the status bar; clicking it opens the endpoint form
  (`Widgets/ConnectionBar`, a flyout).

## Title bar

The main window and the settings window have no OS title bar: their top line is the app's own
`Shell/WindowTitleBar` (title, `LeftContent` / `RightContent` for the window's own buttons,
minimize / maximize / close; `CanMinimize` / `CanMaximize` hide those). A window opts in with
`Classes="appChrome"` (App.axaml: `ExtendClientAreaToDecorationsHint` +
`WindowDecorations="BorderOnly"`, Avalonia 12's client-side decorations, and the
`AppWindowDecorations` theme with frame and shadow thickness 0, so the window ends at the app's
own edges) and docks a `WindowTitleBar` on top. The bar moves the window itself: a press on an
empty part calls `BeginMoveDrag`, a second press within the system double-click time maximizes /
restores. The decoration roles (`ElementRole="TitleBar"`, the button roles) are not used: on Linux
the drag role grabs the mouse (no double-click, clicks on buttons start a drag) and the button
roles do nothing; a press on any button stays with the button. Other dialogs and floating widget
windows keep the OS title bar.

## Layout style (Settings → Appearance)

`LayoutStyle` (`GeneralSettingsEntity`): **Flush** (default; also for settings files saved without
the field) gives every widget the docked look (square, no border; the current one keeps its 1 px
frame) and fills the window edge to edge; **Cards** draws center and floating widgets as rounded
tiles with 6 px gaps between them and around the center. A `WorkspaceViewModel` without an
`AppearanceStore` (offline views) draws cards. `AppearanceStore` follows the saved setting (`AppSettingsChanged`), so Save applies it at
once. Corners and borders come from the `flush` class on the window (`:is(Window).flush
Border.panel` in App.axaml; the main window binds it, `WorkspaceView` sets it on floating windows);
the geometry is `WorkspaceView.ApplyLayoutStyle`: center margin, floating window padding, and
splitters — a 6 px transparent gap in cards, a 1 px line in flush that is grabbed over 3 px on each
side (`FlushSplitterColumnBrush` / `RowBrush`, ZIndex 1 over its neighbours). A collapsed side
leaves its gap in cards and none in flush.

## Display time zone (Settings → General → Time zone)

`DisplayTimeZoneId` (`GeneralSettingsEntity`, a system time zone id): null — the default, also
for settings files saved without the field — is the system's local zone. The picker
(`TimeZoneOption`) lists System local, **Exchange time** (`DisplayTimeConstants.ExchangeTimeZoneId`,
New York) and every zone of the computer; Save validates the id (`SVC_005`).
`DisplayTimeStore` (a store in `LT.Widgets.Core`) holds the resolved `Zone` and `Now` (from
`TimeProvider`), formats times in it, and follows the saved setting: `ZoneChanged` fires on the UI
thread and open widgets format their times again — no restart.

Every shown time goes through it — never `ToLocalTime()` / `DateTime.Now` in display code.
Conversion is `DisplayTimeHelpers.ToZone`; `FormatHelpers.DayTime` gives "14:05" today, the date
another day, "today" counted in the zone. A status text that holds a time is a `ZonedText` (it
keeps how it was built and `Rebuild`s on `ZoneChanged`); rows format their time when built and
are rebuilt or refreshed on `ZoneChanged`; the charts bind `TimeZone="{Binding DisplayTime.Zone}"`.
Daily and weekly chart bars carry a calendar date (midnight UTC) and are shown unconverted.
Business rules keep their fixed zones whatever is picked: trading days
(`TradeJournalConstants.TradeDayTimeZoneId`, `AccountPerformanceConstants.TradingDayTimeZoneId`)
and Flex statements (`FlexConstants.StatementTimeZoneId`).

## Settings window

`Widgets/Settings`: a searchable tree of pages on the left, the selected page on the right. A page
(`SettingsPageViewModel`) has tabs (`SettingsTabViewModel`; the tab strip shows only with more than
one); a group (no tabs) holds child pages and opens its first one. Each setting is a
`SettingItemViewModel` (`BoolSettingViewModel`, `ChoiceSettingViewModel`,
`NumberSettingViewModel`, `TextSettingViewModel` — masked when `isSecret`, optionally normalised as
typed —, `GuideSettingViewModel` for a numbered checklist with values to copy and links to open,
`ActionSettingViewModel` for a status line with a button that acts right away (and `IsBusy`);
one DataTemplate each) with a title, help text and search keywords.
Search (`SettingsSearchHelpers.Matches`: every word, any order) keeps the pages, tabs and settings
that match; a page, group or tab whose own title matches shows all of its settings. Changes apply
on Save. Adding a setting: a field in `SettingsViewModel`, put it on a tab, read and save it there.

Other parts of the app open Settings on a page through `SettingsNavigator` (a store in
`LT.Widgets.Core`): `Open(SettingsPageNames.AccountHistory)` raises `OpenRequested`, the main
window opens the dialog (once) and `SettingsViewModel.ShowPage` selects the page.

**Account history → IB Flex**: the setup as a numbered checklist (`GuideSettingViewModel`,
built by `FlexSetupGuide`; each value has a Copy button, Client Portal steps a button that opens
IB's Flex Queries page in the browser through `Launcher` — see
[ibkr-flex.md → Setup](ibkr-flex.md#setup-once-by-the-user)), the Flex token (masked, whitespace
dropped on paste; stored in `settings.json`), the Activity and the optional Trade Confirmation
query ids (a pasted URL or "Query ID: …" keeps just the number), and **Save & import now**
(an indeterminate progress bar and the running step while it imports), which saves just these three fields (validated by `SaveFlexSettingsRequestValidator`,
messages `FlexValidationCodes`) and runs `IFlexImportManager.ImportNowAsync`. Its status line
(`FlexStatusHelpers.Describe`) follows `IFlexImportManager.StatusChanged`: what is running, the
covered history, the last imports and the last error. Save also saves the three fields; the view
model unsubscribes when the window closes.

## Workspace (`LT.App/Workspace`)

Every widget is **independently spawnable** and lives in one of four wrappers:

| Wrapper | Behaviour |
|---|---|
| **Center** | One or more **views** (tabs above the center), each a split layout of its own: widgets side by side and/or stacked, nested to any depth, with a splitter between every pair. |
| **Left / right side** | Each widget docked there gets an icon on that edge's strip. One is expanded at a time; expanding one (its icon, Ctrl+1…9, the menu) makes it current and gives it keyboard focus; clicking its icon again collapses the side and the center grows. Each widget keeps its own panel width (`WidgetItem.SideWidth`, saved in the layout); one without a width of its own starts at the side's last width. |
| **Bottom** | Like a side, along the bottom of the center column (the side panels keep their full height): a horizontal icon strip, shown only while a widget is docked there, and above it one expanded widget at a time. Each widget keeps its own panel height (`WidgetItem.BottomHeight`); a collapsed bottom leaves no gap. |
| **Floating** | The widget gets its own window (owned by the window of its workspace). Closing that window closes the widget. |

- **Workspace windows.** Menu → New window (Ctrl+Shift+N, from any workspace window) opens an
  empty "Window 2", "Window 3", … with a workspace of its own: its own center views, left /
  right / bottom docks and floating widgets, and its own menu (Refresh all, New window, Widgets,
  Close window) and shortcuts, which act on that window. Widgets picked there open there. Charts
  open independently in every window; a single-instance widget exists once in the app, so
  picking it in window B while it lives in window A (open or closed) **moves** it to B, with its
  settings and view model (`WorkspaceViewModel.Release` in A, adopted through the claim callback
  `WorkspaceWindows` gives every workspace). Link groups and every store stay app-wide, so a
  chart in one window follows a watchlist in another. The windows aren't owned by the main
  window (they can sit on another screen, with a taskbar entry); closing one discards it and
  disposes its widgets; closing the main window exits the app (`ShutdownMode.OnMainWindowClose`)
  and keeps the open windows, with their bounds and layouts, for the next start. Dragging a
  widget between windows is not supported: dropped outside its window, it floats (owned by that
  window); move it by opening it from the other window's menu (single-instance widgets).
  `WorkspaceWindows` (singleton) holds the main workspace and the extra ones
  (`WorkspaceWindowViewModel`, shown by `Shell/WorkspaceWindow`, which the main window opens).

- **Center views.** A tab strip above the center lists the views; left, right, bottom and
  floating widgets are shared by all of them, a center widget sits in exactly one. Click a tab to
  show it, double-click to rename it in place (Enter or clicking away saves, Esc cancels, a blank
  name keeps the old one), ✕ closes the view together with its widgets (like ✕ on each; the last
  view has no ✕). Right-click a tab → Duplicate puts "<name> copy" right after it and shows it:
  the same split tree with new instances of its widgets carrying their saved settings (symbol,
  timeframe, link group, …); single-instance widgets can't be in two views, so their slots are
  left out (`CenterViewHelpers.Duplicate`). "+" adds an empty "View N" and shows it; Ctrl+Tab / Ctrl+Shift+Tab cycle
  through them. New and reopened center widgets go to the shown view; showing a widget that sits
  in another view (menu, Ctrl+1…9, Ctrl+Alt+key) switches to that view. Every view has its own
  host in `WorkspaceView`, built once per tree change and only hidden while another view is
  shown, so switching views moves no frame. Tabs can't be reordered yet.
- Spawn or reveal a widget from menu → Widgets. Ctrl+1 … Ctrl+9 do the same, but collapse a side
  widget that is already expanded.
- **Charts can be open several times**: menu → Widgets → Chart (Ctrl+7) always opens a new one
  in the current view. Each chart has its own timeframe, link group and contract, all saved per
  chart. New charts start at the last chart's timeframe (5m on first run). **Changing the
  timeframe:** click the timeframe beside the chart's header title (e.g. "5m"); a menu lists 1m,
  5m, 15m, 1h, 1D and 1W with the current one checked, and picking one reloads the bars
  (historical + real-time) at that size. The other header controls (RTH, Reload) are hidden until
  they are redesigned; F5 reloads.
- **Picking a chart's ticker:** click the chart's header title (`ChartTitleView`, hosted in the
  frame header via `WidgetViewFactory.CreateTitle`). It turns into a ticker box: typing lists
  IB's listings of the symbol to pick from, Enter takes the highlighted one or the typed ticker's
  first listing (`IContractManager.ResolveTickerAsync`); an empty box (or the chevron) lists the
  last 10 tickers (`SettingsEntity.RecentContracts`, shared by all charts). Esc or clicking away
  cancels.
- **Ticker search boxes** (chart title, watchlist) are one control, `Controls/ContractSearchBox`.
  A typed symbol is looked up as stock / ETF, index and crypto at once
  (`IContractManager.SearchSymbolAsync`, `MarketDataConstants.SymbolSearchTypes`), US-dollar
  listings first. Futures and forex are not searched (every contract month / currency pairs).
  A ticker picked this way goes to the chart's link group. Closing an extra chart
  discards it (its view model is disposed); single-instance widgets keep theirs while closed.
  Which widgets allow several instances: `WorkspaceHelpers.AllowsMultipleInstances`.
- Every widget is wrapped in a `WidgetFrame`: icon, title, link, fullscreen, `✕` (close) and, on
  a side, the pin. **The header is the drag handle**, and so is the strip icon of a widget docked
  on a side or the bottom (no need to expand it first): a press becomes a drag once it moves
  6 px (`WorkspaceConstants.DragThreshold`); a click that doesn't still toggles the widget, a drag
  doesn't. Both handles share `Workspace/DragGesture`. While dragging, a highlight shows the target:
  - left / right: the strip (+24 px), or the open side panel → dock on that side **at an icon
    position**: the strip's icons part to open a gap at the slot nearest the pointer and the gap
    is highlighted; dropping inserts the icon there (also reorders icons within a strip);
  - bottom: the bottom strip or the open bottom panel → dock at the bottom at an icon position
    (the strip's icons part sideways); with nothing docked there yet, a 24 px band along the
    center's bottom edge ("Dock at the bottom");
  - a center widget → the **nearest edge** decides: left / right of it, **above / below** it
    (that half lights up). Dropping in the same direction as its row/column joins it; otherwise
    the widget's cell is split;
  - empty center → fill it;
  - a view tab → into that view, right of everything, and the view is shown. Holding the drag
    over another view's tab for 0.6 s (`WorkspaceConstants.ViewTabHoverSwitchMs`) shows that view
    so the widget can be dropped anywhere in it; hidden hosts stay in the visual tree, so the
    dragged frame keeps its pointer capture even when its own view is hidden;
  - outside the main window → float, with the window placed under the pointer (dragging a
    floating widget's header outside just moves its window).
- Drag positions travel in screen pixels (`PointToScreen` / `PointToClient`), so a drag can start
  in a floating window and end in the main one (`WorkspaceView.DragDrop.cs`).
- A frame never moves between windows in one step: the old window is closed (or the frame
  detached) first and the frame is re-hosted on a later dispatcher pass. Moving a control into
  another window while the old one still has it queued for layout aborts the process
  ("InvalidateArrange on wrong LayoutManager").
- No hover cursor or tooltip on headers; the cursor turns into a move cursor only while dragging.
- Docked on a side, a frame sits flush against the strip and the window edges (no margins,
  no rounded border — `Border.panel.docked`); center tiles and floating widgets keep the card look.
- A widget opens where it last was; a new chart opens where the last chart was. On first run
  (`WorkspaceHelpers.GetDefaultPlacements`, sizes in `WorkspaceConstants`) the window is maximized
  with every side collapsed: Chart in the center on SPY at 5m (link group red); Watchlist on the
  left (unpinned); Account and Messages (pinned), Option chain and Order ticket (unpinned) on the
  right. Positions, Orders, Executions, News, Account performance and Day trade performance are
  closed.
- **Order ticket** (`LT.Widgets.OrderTicket`, right side): takes its contract from the link group.
  Account, side, market / limit, quantity or amount in $ (market only), limit price, time in force
  (crypto: GTC / IOC). "Review order" validates (`IOrderManager.ValidateOrder`) and spells the
  order out with the endpoint; any edit drops the review; only "Send order" sends. Orders sent
  this session list their status below, with Cancel while working; everything is also logged
  to Messages.
- **Close position** (Positions, a Close button per live row; disabled for remembered rows and
  while disconnected): opens the Order ticket's own view and view model in a dialog
  (`LT.App/Widgets/OrderTicket/OrderTicketWindow`) through `OrderTicketNavigator` (a store in
  `LT.Widgets.Core`, the main window listens like `SettingsNavigator`). The draft
  (`OrderHelpers.GetClosingDraft`) is the position's account and contract, sell for a long / buy
  for a short, the whole size, at market. `OrderTicketViewModel.Prefill` leaves the link group
  so the popup can't switch contract; review and send are the ticket's own — nothing is sent
  until "Send order". The view model is disposed when the dialog closes.
- **Quick exit** (Positions, a lightning button next to Close; same enabling): sends a market
  order for the whole position **right away**, no ticket and no confirmation —
  `IOrderManager.ExitPositionAsync` with `OrderHelpers.GetQuickExitRequest` (the position's
  account and contract, sell a long / buy a short, the whole size, market, the security type's
  default time in force: DAY, IOC for crypto). The row's button is disabled from the click until
  the order fails or ends unfilled, or the position next changes (a fill waits for the position
  update, so a stale size is never sent twice); `OrderManager` (a singleton) also refuses a second
  exit of a position while one is working, across widgets. Sent / done / rejected go to Messages;
  failures also show in the widget's error line.
- **News** (`LT.Widgets.News`, bottom, Ctrl+Alt+N): two modes, saved per widget with the provider
  selection (`WidgetPlacementDto.NewsMode` / `NewsProviders`, keys `source:code`, null = all).
  *Linked* follows the link group's contract: the latest 100 past headlines, then live ones
  (Load older pages back). *All news* subscribes to the broad tape of each selected provider that
  has one (chips without one are dimmed in that mode). Provider chips choose what is asked for
  (changes within 400 ms restart the feed once); the text box filters the list locally. Rows:
  time, provider badge, headline, sentiment ▲/▼ when the provider scores it; newest first, at
  most 500 (live headlines push the oldest out). Selecting one loads its article into the
  reading pane as plain text (`NewsHelpers.ToPlainText`, no HTML renderer); a document (PDF) gets
  "Open document", which writes it to the temp folder and opens it with the OS. Live headlines
  are read on the thread pool and posted to the UI in batches. Data comes from `INewsManager`,
  which merges every `INewsSource` (IB now) and dedupes on (source, provider, article id).
- **Day trade performance** (`LT.Widgets.DayTradePerformance`, center, closed on first run,
  Ctrl+Alt+J): one trading day of the selected account from the trade journal
  ([ibkr-tws-api.md → Trade journal](ibkr-tws-api.md#trade-journal)) — ‹ / › step over recorded
  days, Today goes back. The intraday P&L curve (`PnlCurveChart`, drawn like the candlestick
  chart; live for today) with session high / low and max drawdown from a peak; then net P&L,
  trades (W / L), win rate, average win / loss, profit factor, expectancy, largest win / loss,
  gross and commissions, and the day's closed trades newest first. Recording runs whether the
  widget is open or not; recorded days show offline. Fills imported from IB Flex count like
  recorded ones, and ‹ / › reach every day with fills, so the stats cover days before the app
  first ran. The shared `FlexSetupBanner` (`LT.Widgets.Core/Controls`) sits on top while
  Flex isn't set up or needs the user, with Set up… opening Settings on its page; a small note
  says where today's trades come from ("via IB Flex, up to ~5 min delayed",
  `FlexStatusHelpers.DescribeTodayTrades`) or, without Flex, why IB's executions failed, if they did. F5 also
  starts a Flex import.
- The frame (and the widget view inside it) is created once per instance and **moved** between
  hosts, so state and subscriptions survive docking, floating and collapsing. Widget view models
  are created on first open by `WidgetViewModelFactory`.
- `WorkspaceViewModel` owns placement: the center views (`Views`, `CenterViewItem`s each holding
  a split tree `Root`, a `CenterNodeDto` edited by `CenterLayoutHelpers`) and the shown one
  (`SelectedView`; `CenterRoot` is its tree), the `Left` / `Right` / `Floating` collections and
  the expanded widget per side. `WorkspaceView` only mirrors it: each view's host is rebuilt as
  nested grids whenever a tree changes, splitter sizes are written back into the tree after a
  drag.
- Saved in `LayoutEntity` (its workspace part is `WorkspaceLayoutEntity`, shared with every
  extra window's `WorkspaceWindowEntity` in `LayoutEntity.Windows`, which adds its id, name and
  bounds; layouts saved before windows have none): `Widgets` (id, kind, dock, floating bounds, chart settings of each open
  instance), `Views` (id, name and split tree with sizes of each view) with `SelectedViewId`, the
  expanded widget per side and side widths. Layouts from before views have only `Center` (one
  split tree): it becomes the first view, "Main", on load and is saved as null afterwards
  (`CenterViewHelpers.Normalize`, which also keeps each center widget in exactly one view).

**Adding a widget** (in its own project, like `LT.Widgets.Watchlist`): a `WorkspaceTool` value
(at the end); a `src/Widgets/LT.Widgets.<Widget>` project referencing `LT.Widgets.Core`, added to the
solution's `Widgets` folder and referenced by `LT.App`; an `IWidgetDefinition` there (title, icon, focus key, view model type,
view and optional header title) listed in `WidgetCatalog.Definitions` — that also registers its
view model in DI; a default dock in `WorkspaceHelpers.GetDefaultDock`, and a menu entry in
`WidgetsMenuItem.Order` (the Widgets menu of every window). Per-instance settings: implement `IWidgetState` on the view model.

`WidgetCatalog`, `WidgetViewFactory` and `WidgetViewModelFactory` only read the definitions; a
widget's header title is its definition's `CreateTitle()` (`WidgetTitles.Bound` for plain text bound
to the view model).

ViewModels are resolved from DI. They have no interfaces, so the LSCore convention scan skips them —
register each one as transient in `Program.cs` (`builder.Services.AddTransient<FooViewModel>()`);
widget definitions' view models are registered from `WidgetCatalog.Definitions`.
Row VMs (`WatchlistRowViewModel`, `PositionRowViewModel`, …) are constructed by their parent
instead.

### Watchlists

The watchlist widget shows one of several named watchlists. Its header (`WatchlistTitleView`) is
the shown watchlist's name; clicking it opens the list of all of them, with symbol counts. Each row
is picked by clicking its name, renamed in place with the pencil (Enter or clicking away saves,
Esc cancels) or deleted with ✕ after an inline confirmation (disabled when only one is left); the
last row, "+ New watchlist", adds "Watchlist N" and opens its name for editing. Rows are
`WatchlistItemViewModel`s holding their own rename / confirm state; the list lives in a popup, so
row buttons bind to `Owner` (the widget's view model) commands. Adding and removing symbols works
on the shown watchlist. `IWatchlistManager` keeps them in `SettingsEntity.Watchlists` with
`ActiveWatchlistId`; the single list of older settings becomes "Main" on first load
(`WatchlistHelpers.EnsureWatchlists`). A fresh install (no settings file) starts with a "US" list
of SPY, MSFT, NVDA and TSLA (`WatchlistHelpers.CreateFirstRunSettings`).

The **Mkt Cap** column is the SEC's shares outstanding × the row's last price (previous close
before the first trade), recomputed on every UI flush and sortable by its numeric value
(`MarketCapValue`). Only US stocks (STK in USD) get one; anything else, and stocks the SEC has no
usable count for (ETFs, foreign filers), show "—". Starting a row's quote also starts its
`ISharesOutstandingManager` lookup (cached on disk, so normally instant); the result is posted to
the row on the UI thread. Columns are fixed: nothing about them is persisted.

### Pinned side widgets

A widget docked on a side has a pin in its header. Pinned (the default) it stays expanded until
you click its strip icon; unpinned (tilted pin) it collapses as soon as you click or move
focus anywhere outside its panel — another widget, the toolbar, a floating window. Its own strip
icon, splitter and dropdowns don't count. `WidgetItem.IsPinned`, saved in the layout;
`WorkspaceView` watches clicks and focus on the window (tunnelling handlers).

**Esc** in an expanded side or bottom widget hands keyboard focus back to the center widget
focused last (or the first center widget, if none was), which becomes current; an unpinned
widget also collapses, a pinned one stays open. Only an Esc nothing inside the widget handled
counts — an open dropdown, a ticker box or a rename box keeps its own Esc
(`WorkspaceView.OnDockedKeyDown`, bubbling, → `WorkspaceViewModel.LeaveDocked`; the target is
`WorkspaceHelpers.GetFocusReturnId`).

### Fullscreen

The fullscreen button next to a widget's ✕, or Ctrl+Shift+F, shows the widget over the whole
window (toolbar and status bar included), framed in orange; the same button or shortcut restores it.
One widget at a time. `WorkspaceViewModel.FullscreenWidget` holds it; `WorkspaceView` moves its frame into the
window's fullscreen layer (`IFullscreenHost`, implemented by `MainWindow`) and back, so the widget
keeps its state. A floating widget maximizes its own window instead. Moving or closing a
fullscreen widget restores it first; its header doesn't start a drag while fullscreen.

### Link groups

Watchlist, Positions, Orders, Executions, Chart, Option chain, Order ticket and News can be linked:
the chain icon left of the frame's close button opens a menu of ten color groups (Red, Green, Blue, Yellow, Orange, Purple, Cyan,
Pink, Lime, White) or Not linked, and is tinted
with the current one. Widgets in the same group show the same contract:

- Clicking a watchlist, positions, orders or executions row, or picking a chart's ticker, sends
  that contract to the group; charts and option chains in the group switch to it. Remembered
  (stale) positions, orders and fills send nothing. Orders and Executions rebuild their list on
  every refresh and keep the same row selected (by permId / execId) without sending it again, so
  a refresh never pulls the group back. An execution's contract goes out without the fill's
  venue, so linked widgets ask through SMART.
- Positions, Orders and Executions only send: a contract from the group doesn't filter or move
  their lists.
- Joining a group shows the group's contract; joining an empty group gives it the widget's own.
- New widgets start in Red, so out of the box they all follow each other. "Not linked" keeps a
  widget on its own contract.

The option chain has no ticker input of its own: its underlying comes from its group, and the
frame header shows that ticker (`WidgetViewFactory.CreateTitle` binds `OptionChainViewModel.Title`).
It lists one tab per expiration (`IOptionChainManager.GetExpirationsAsync`, from
`reqSecDefOptParams`); the selected tab loads that day's strikes around the money. Columns mirror
around the strike with the bid next to it on both sides: call vol · last · ask · bid | strike |
put bid · ask · last · vol. The
underlying keeps streaming while a chain is shown: in-the-money cells are shaded per side (calls
below the price, puts above it — `OptionChainHelpers.IsInTheMoney`, `ItmBrush`) and the strike
nearest the price is marked ATM (`AtmBrush`); with no known price nothing is shaded. The
window of strikes (8 each side) follows the price too: once the ATM strike is more than 3 strikes
off its centre (`OptionStrikesRecenterThreshold`, so a price wobbling around a strike doesn't
churn lines) it re-centres (`OptionChainHelpers.GetRecenterTarget`). Rows that stay keep their
view models and quotes; the lines of rows leaving are released before the new rows subscribe, so
the chain never holds more than a full window plus the underlying. The grid scrolls the new ATM
row into view.

Prices, strikes and the underlying in the info line always show 2 decimals
(`FormatHelpers.FixedPrice`), volume a whole number. The strike column is coloured by distance
from the live price in standard deviations of the expected move to the selected expiration:
1σ = price × IV × √T (`OptionChainHelpers.GetExpectedMove`), T in calendar years to 16:00 New
York on the expiry date (`GetExpirationTime`, via `TimeProvider`). Bands ≤ 1σ, ≤ 2σ, ≤ 3σ, > 3σ
(`GetSigmaBand`, `SigmaOneBrush` … `SigmaBeyondBrush`) fade out with distance; the ATM strike
keeps an accent outline on top and the strike tooltip shows the distance ("+1.4σ from the
underlying"). IV is the underlying's 30-day option implied volatility — generic tick 106 →
tick 24, asked for on the underlying's existing quote line, so no extra line — not the ATM
option's own IV for that expiry: IBKR.Net doesn't decode option computation ticks
(`tickOptionComputation`) yet. No IV, no price, or past expiry → no band colouring.

**One-click trading (real orders, no confirmation).** In the option chain a single click on an
**ask** cell buys and on a **bid** cell sells — calls and puts alike — 1 contract, market, DAY, on
the account selected in `AccountStore` (the one the Order ticket starts on). The order is sent
immediately through `IOrderManager.PlaceOrderAsync` (same validation as the ticket); there is no
review step. Only bid / ask cells are clickable: they show a hand cursor and a translucent green
(buy) / red (sell) tint on hover over the ITM shade; last, volume and strike cells don't react.
A cell is disabled while disconnected, when its side has no contract or that column has no price,
and while its own order is in flight. Repeat clicks on the same cell within 500 ms
(`OrderConstants.QuickOrderRepeatWindow`) are dropped, so a double click sends one order. The
chain's status line shows "Order n sent" for a few seconds and the fill; a failed send or a
rejection shows in its error line, and everything is logged to Messages (Orders). Mapping and
guard logic: `OptionChainHelpers.GetQuickOrderAction` / `BuildQuickOrder` / `CanQuickTrade` /
`IsRepeatClick`.

A linkable view model implements `ILinkable` (`LT.App/Workspace`) by holding a `ContractLink`
(`src/Widgets/LT.Widgets.Core/Stores`): `Publish(contract)` to send, `ContractReceived` to follow; it never gets its
own publish back. The frame shows the link button for any `ILinkable`, and the workspace saves
the group in `WidgetPlacementEntity.LinkGroup`. Layouts from before link groups load pinned
charts as Not linked.

### Dragging a ticker onto a chart

A row of the Watchlist, Positions, Orders or Executions can be dragged onto any chart: the
chart the ticker is dropped on shows it, like one picked in its own ticker box (it also goes to
the recent tickers). Only that chart switches: the drop doesn't touch the link group, so other
charts keep their contract. The chart stays in its group and follows the group's next contract
(the group store skips a contract it already holds, so re-clicking the group's current row
doesn't pull the chart back). Remembered (stale) positions, orders and fills don't drag, like
they don't link. The press still selects the row (and so sends it to the source's group).

- A left press on a row turns into a drag once it moves 6 px (`WorkspaceHelpers.IsBeyondDragThreshold`,
  like widget headers); clicks, double-clicks, scrolling and presses on buttons inside a row
  (Close, Quick exit, Cancel) never start one.
- While a ticker hovers a chart, the chart shows the `Border.dropTarget` highlight with
  "Show AAPL"; anything else dragged over it is refused.
- Plumbing in `LT.Widgets.Core`: `Helpers/TickerDragDrop` — attached `IsSource` on the list
  (its view model implements `Workspace/ITickerDragSource`, which says what contract a row
  item carries), `IsTarget` on the drop area (its view model implements `ITickerDropTarget`),
  and `HoverText` for the highlight to bind to. The payload is the row's `ContractDto` in an
  in-process `DataFormat` (Avalonia 12 `DataTransfer` / `DragDrop.DoDragDropAsync`), so it never
  reaches other applications.

### Account performance

`LT.Widgets.AccountPerformance` (opens in the center; menu → Widgets, Ctrl+Alt+R) shows the
account picked in `AccountStore`: return stats, an equity curve, daily P&L bars and a daily P&L
calendar, with a 1W / 1M / YTD / All range for the two charts and the max drawdown. IB's socket
API has no account history: past days are imported from IB Flex (end-of-day NAV and deposits /
withdrawals, back to the account's start — [ibkr-flex.md](ibkr-flex.md#merge-rules); such days
have `Source = Flex` and win over recorded ones), today is recorded live:

- **Recording** — `IAccountPerformanceManager` (singleton, run for the app's lifetime by the
  `AccountPerformanceRecorder` hosted service, so it records whether or not the widget is open)
  follows the account summary and account P&L of every managed account while connected — the same
  shared IB subscriptions as the Account widget — and keeps one `AccountDaySnapshotEntity` per
  account and trading day: net liquidation, currency, daily / realized / unrealized P&L, first
  and last update. Later values of the day overwrite earlier ones. Written to
  `account-history/<account>.json` at most every 30 s (`AccountPerformanceConstants.SaveInterval`),
  and on disconnect and exit. A new day starts with the last net liquidation seen (the summary only
  arrives when it changes).
- **Trading day** — the date in New York (`AccountPerformanceConstants.TradingDayTimeZoneId`), for
  every user: one US session never splits across two days. Late evening in Asia, or after
  midnight in Europe, is still the previous day.
- **Returns** — from each day's P&L, never from raw net liquidation deltas, so deposits and
  withdrawals don't count: a recorded day uses IB's daily P&L, a Flex day its NAV change minus the
  day's net deposits (`CashFlow`). A day's return is its daily
  P&L over the equity it started with (net liquidation minus the day's P&L); periods chain those
  (time-weighted). Money figures sum the daily P&L. Max drawdown: of the summed P&L curve (money)
  and of the chained returns (%), over the picked range. Days the app didn't run are missing,
  not interpolated. All of it is in `AccountPerformanceHelpers` (`LT.Contracts`).
- **Live** — today's row follows every P&L tick (`HistoryChanged`, coalesced to one reload per
  dispatcher pass). The same `FlexSetupBanner` as Day trade performance sits on top while Flex
  isn't set up or has an error the user must fix; the footnote only says where history comes from
  and since when, and what the import is doing. F5 also starts a Flex import if one is due.
- The equity curve places days by date (gaps where there is no data) and plots net liquidation,
  so a deposit shows as a step there (not in returns or P&L); the P&L bars are spaced evenly, one
  per day with data; the calendar is six Monday-first weeks.

## Connected widgets

Widgets that only work while connected derive from `ConnectedViewModelBase`. It gives them
`IsConnected`, `ErrorMessage` and a **session token**: `OnConnected(session)` runs on every
(re)connect, the token is cancelled on disconnect, and `RestartSession()` restarts the work (e.g.
when the selected account changes). `RunPeriodicAsync` covers poll-style data (account summary
every 3 min — IB only refreshes it that often, orders every 10 s, executions every 30 s).

## Threading — the one rule that matters

IB callbacks and `IAsyncEnumerable` continuations run **off the UI thread**. Never touch an
observable property or collection bound to the UI from there:

```csharp
connectionManager.StatusChanged += status => Dispatcher.UIThread.Post(() => Apply(status));
```

For high-frequency streams (ticks) don't post per tick — coalesce. The pattern used by the
watchlist and the option chain:

1. Consume the stream on the thread pool: `Task.Run` + `ConfigureAwait(false)`.
2. Write each quote into the row's `LatestValue<QuoteDto>` (`Interlocked.Exchange`, newest wins).
3. A `DispatcherTimer` (250 ms) calls `row.Flush()`, which takes the value and updates the bound
   strings once.

Low-frequency streams (positions, P&L, 5 s bars) are consumed directly on the UI thread — the
`await foreach` continuation comes back on the Avalonia synchronization context.

## Lifetime of subscriptions

A widget that subscribes to a stream holds a `CancellationTokenSource`, cancels it when the
connection drops or the widget is closed. Cancelling ends the `await foreach`, which cancels the IB
subscription and frees the market-data line.

## Keyboard shortcuts

| Keys | Action |
|---|---|
| Ctrl+F | Focus the watchlist's add-symbol box |
| Enter / Esc (ticker box) | Take the highlighted listing (or the typed ticker) / cancel |
| Delete (watchlist) | Remove selected row |
| F5 | Refresh chart, account, positions, orders, executions, day trade performance |
| Ctrl+, | Settings |
| Ctrl+1 … Ctrl+9 | Show Watchlist, Positions, Orders, Executions, Account, Messages, Chart, Option chain, Order ticket (collapses an expanded side widget); Ctrl+7 always opens a new chart in the current view |
| Ctrl+Alt+W / P / O / E / A / M / C / B / N / R / J | Show and focus Watchlist, Positions, Orders, Executions, Account, Messages, Option chain, Order ticket, News, Account performance, Day trade performance (single-instance widgets; works from floating windows too) |
| Esc (side or bottom widget) | Back to the last focused center widget; an unpinned widget collapses |
| Ctrl+Tab / Ctrl+Shift+Tab | Next / previous center view |
| Ctrl+Shift+F | Fullscreen the current widget (the one last clicked or typed in, framed in light blue), or restore it |
| Ctrl+Shift+N | New empty workspace window |

Ctrl+Shift+*key* combos act on a widget.

The window handles them in a tunnelling `KeyDown` handler (`MainWindow.axaml.cs`, and
`WorkspaceWindow.axaml.cs` for its own workspace — everything but Ctrl+,), so they work
while a text box has focus. On macOS, Cmd works in place of Ctrl.

## Charts

`Widgets/Chart/CandlestickChart` is a custom `Control` drawn with `DrawingContext` (no chart
library): candles, volume, price grid, last-price line, crosshair + OHLCV readout. Wheel zooms,
drag pans, double-click resets (zoom and pan, not the right margin). The account performance widget's `EquityCurveChart` and
`DailyPnlBarChart` are drawn the same way (same palette, `ChartDrawing`).

**Right margin.** A small grip in the time axis marks the end of the last bar; dragging it left
leaves empty space after the latest bar (dragging right narrows it, double-clicking it removes
it), with a resize cursor and a dashed guide through the plot while hovered or dragged. The
margin is kept in bars (`ChartRightMargin`, saved per chart in the layout), so it holds across
zoom, new live bars and timeframe or contract switches; it is capped at half the plot width at
the current zoom (`ChartConstants.MaxRightMarginFraction`). Panning into history uses up the
margin first; the grip is hidden once the last bar is off the plot. Price autoscale still uses
only the visible bars. The viewport math is in `ChartViewportHelpers`.

**Your trades on the chart.** The chart draws the selected account's (all accounts when none is
selected) recorded fills of the charted contract from the trade journal
(`ITradeJournalManager.GetContractTrades`: socket executions and IB Flex history, so only what
was recorded or imported): a green ▲ for a buy and a red ▼ for a sell, its tip at the fill
price, on the bar that contains the fill's time (`ChartTradeHelpers.FindBarIndex`; daily and
weekly bars by the New York date). Fills outside the loaded bars or in a gap (outside RTH)
are not drawn. Each closed quantity is joined entry → exit by a dashed line per FIFO match
(`TradeMatchingHelpers.MatchLegs`), green when it made money net of commission, red when not;
a match with an end outside the loaded bars is not drawn. Hovering a bar lists its fills under
the OHLC readout. With a live open position in the contract an amber dashed **Break-even**
line sits at IB's average cost per unit (divided by the multiplier for options and futures,
commissions included) with its price on the axis; it is hidden while disconnected (no stale
positions) and when off the visible price range. Each chart follows positions on its own
`reqPositions` stream; new fills appear when the journal records them (every 30 s).

## Styling

- Colours/brushes as resources in `App.axaml`; positive/negative P/L via the `positive` /
  `negative` classes, never hard-coded in a view.
- Widgets sit in a `Border.panel` with a `TextBlock.panelTitle` header.
