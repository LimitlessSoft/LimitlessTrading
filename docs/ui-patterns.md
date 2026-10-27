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

## Windows and the title bar

**Every window the app opens derives from `AppWindow`** (`LT.Widgets.Core/Controls`) — the main
and workspace windows, Settings, dialogs (`ConfirmDialog`, Gateway log-in, the order ticket popup,
chart indicator settings) and floating widget windows. Never derive from `Window` directly and
never copy the chrome into a window: in XAML the root is `<controls:AppWindow>`
(`xmlns:controls="using:LT.App.Controls"`), in code `: AppWindow`. Message boxes and "are you
sure?" prompts go through `ConfirmDialog`, not a new window type.

None of them has an OS title bar. The `AppWindow` ControlTheme (App.axaml) turns on
`ExtendClientAreaToDecorationsHint` + `WindowDecorations="BorderOnly"` (Avalonia 12's client-side
decorations) with the `AppWindowDecorations` theme (frame and shadow thickness 0, so the window
ends at the app's own edges), and its template — Fluent's Window template with the bar added —
docks a `WindowTitleBar` above the content. `Content` stays the window's own content and `Padding`
pads only it. The bar shows `Title`, and `TitleBarLeftContent` / `TitleBarRightContent` for the
window's own buttons (the main window's menu and update pill). Its buttons follow the window:
minimize shows with `CanMinimize` and `ShowInTaskbar` (a window without a taskbar entry would have
nothing to come back from), maximize — and double-click on the bar — with `CanMaximize` and
`CanResize`; close always. Modal dialogs set `CanMinimize="False"`, fixed-size ones
`CanResize="False"` with `SizeToContent`, which measures the bar too.

The bar moves the window itself: a press on an empty part calls `BeginMoveDrag`, a second press
within the system double-click time maximizes / restores. The decoration roles
(`ElementRole="TitleBar"`, the button roles) are not used: on Linux the drag role grabs the mouse
(no double-click, clicks on buttons start a drag) and the button roles do nothing; a press on any
button stays with the button. In-window flyouts and menus are not windows and keep their
popups.

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

Every widget is **independently spawnable, any number of times** (menu → Widgets always opens a
new instance) and lives in one of four wrappers:

| Wrapper | Behaviour |
|---|---|
| **Center** | One or more **views** (tabs above the center), each a split layout of its own: widgets side by side and/or stacked, nested to any depth, with a splitter between every pair. |
| **Left / right side** | Each widget docked there gets an icon on that edge's strip. One is expanded at a time; expanding one (its icon, Ctrl+1…9, the menu) makes it current and gives it keyboard focus; clicking its icon again collapses the side and the center grows. Each widget keeps its own panel width (`WidgetItem.SideWidth`, saved in the layout); one without a width of its own starts at the side's last width. |
| **Bottom** | Like a side, along the bottom of the center column (the side panels keep their full height): a horizontal icon strip, shown only while a widget is docked there, and above it one expanded widget at a time. Each widget keeps its own panel height (`WidgetItem.BottomHeight`); a collapsed bottom leaves no gap. |
| **Floating** | The widget gets its own window (owned by the window of its workspace). Closing that window closes the widget. |

- **Workspace windows.** Menu → New window (Ctrl+Shift+N, from any workspace window) opens an
  empty "Window 2", "Window 3", … with a workspace of its own: its own center views, left /
  right / bottom docks and floating widgets, and its own menu (Refresh all, New window, Widgets,
  Close window) and shortcuts, which act on that window. Widgets picked there open there, as new
  instances of that window; every window can hold its own instances of any widget. Link groups
  and every store stay app-wide, so a chart in one window follows a watchlist in another. The windows aren't owned by the main
  window (they can sit on another screen, with a taskbar entry); closing one discards it and
  disposes its widgets; closing the main window exits the app (`ShutdownMode.OnMainWindowClose`)
  and keeps the open windows, with their bounds and layouts, for the next start. **Dragging a
  widget onto another workspace window** moves it there: the same drop targets as in its own
  window (strip slots, beside / above / below a center tile, a view tab) show on that window,
  and anywhere else in it (its title bar, the main window's toolbar) puts it right of everything
  in its shown view. It keeps its view model and settings; the other window builds its own frame,
  a floating widget's window closes first, an id taken there gets a new one, and an extra window
  left with nothing open is closed. Dropped outside every workspace window it floats (owned by
  its own window). Model: `WorkspaceViewModel.MoveWidgetTo` → `IWorkspaceHost.MoveWidget` →
  `ReleaseWidget` / `AdoptWidget`; the drag is `WorkspaceView.DragDrop.cs`. Over overlapping
  windows the dragging window wins (z-order is not looked at), like a dragged view tab.
  **✕ with several workspace windows open** (main + extra; floating widget windows don't count)
  asks: Close this window (Enter), Close application, or Cancel. With one window, ✕ just exits.
  The extra window's menu → Close window doesn't ask. Close application exits like closing the
  main window always did, keeping every window. Close this window on an extra window discards it
  as before. The main window can't go (it holds the toolbar, status bar and connection, and the
  app exits with it), so there it **takes over the first extra window**: that window's workspace
  moves into the main window (`WorkspaceWindows.ReplaceMain`, raising `MainChanged`; the page
  rebinds), the main window takes its bounds and state, that window closes
  (`WorkspaceWindow.HandOver` releases its frames first) and the old main workspace is discarded
  with its widgets. `WorkspaceView` empties its hosts whenever its workspace is swapped
  (`ClearHosts`), so the new one gets fresh frames.
- **Window placement.** The main window, every extra workspace window and every floating
  widget window come back on the screen they were on, at their normal-state bounds and in their
  state (normal, maximized, full screen; minimized comes back as the state before it).
  `WindowPlacementTracker` (`LT.App/Shell`, one per window) follows the window while it is open:
  normal bounds are taken only once the platform has settled and the window is still normal, so
  maximized bounds never count and un-maximizing after a restart goes back to the right size.
  `Capture` gives what is saved (on close and when the app exits, with the layout);
  `Restore` sets the normal bounds first and the state once the window is shown, so a maximized
  window maximizes on its own screen. The screen math is `WindowPlacementHelpers`
  (`LT.Contracts`): the saved screen (name and pixel bounds) is found again by name and bounds,
  else by a unique name (resolution or arrangement changed), else by unique bounds (renamed); the
  window keeps its spot relative to that screen's corner; a screen that is gone puts it on the
  primary screen at the same relative spot. Without a saved screen (older layouts) it goes on the
  screen it overlaps most, or centered on the primary one; either way it is clamped into the
  working area and shrunk to fit. Positions are device pixels, sizes DIPs (converted with the
  target screen's scaling). Older layouts without a position or state open as before (main
  window centered, `IsMaximized` decides).
  `WorkspaceWindows` (singleton) holds the main workspace and the extra ones
  (`WorkspaceWindowViewModel`, shown by `Shell/WorkspaceWindow`, which the main window opens).

- **Center views.** A tab strip above the center lists the views; left, right, bottom and
  floating widgets are shared by all of them, a center widget sits in exactly one. Click a tab to
  show it, double-click to rename it in place (Enter or clicking away saves, Esc cancels, a blank
  name keeps the old one), ✕ closes the view together with its widgets (like ✕ on each; the last
  view has no ✕) after a confirmation (`Controls/ConfirmDialog` in `LT.Widgets.Core`: a modal with
  Cancel and a red confirm button; Enter confirms, Esc cancels). Right-click a tab → Duplicate
  puts "<name> copy" right after it and shows it:
  the same split tree with new instances of its widgets carrying their saved settings (symbol,
  timeframe, link group, watchlist, …) (`CenterViewHelpers.Duplicate`). "+" adds an empty "View N" and shows it; Ctrl+Tab / Ctrl+Shift+Tab cycle
  through them. New center widgets go to the shown view; showing a widget that sits only
  in another view (Ctrl+1…9, Ctrl+Alt+key) switches to that view. Every view has its own
  host in `WorkspaceView`, built once per tree change and only hidden while another view is
  shown, so switching views moves no frame.
- **Moving views.** Drag a tab (a press that moves 6 px, like a widget header; not while its name
  is being edited) along the strip to reorder: a bar marks the gap it goes to. Dropped on another
  workspace window it moves there — into the gap when dropped on that window's tabs, after its
  tabs anywhere else in it (the window is highlighted) — and is shown there. Dropped outside every
  window it is **detached**: a new window ("Window N", opened under the pointer) holding only that
  view. Released over its own window off the tabs, nothing happens. Right-click a tab for the same
  without dragging: Move left / Move right, Move to window ▸ (every other window, main first) and
  Detach to new window. A moved view keeps its id, name, split tree with sizes and its widgets'
  view models and settings (symbol, link group, …); the other window builds new frames and widget
  views for them (nothing is re-hosted across windows), and a widget whose id is taken there gets
  a new one. Moving the last view out of a window leaves an empty "Main" view there; an extra
  window left with nothing open is closed (the main window stays). Model:
  `WorkspaceViewModel.MoveView` / `MoveViewBy` (reorder), `ReleaseView` → `CenterViewTransfer` →
  `AdoptView` (between workspaces), orchestrated by `WorkspaceWindows` through `IWorkspaceHost`
  (`MoveView`, `DetachView`); the drag is `WorkspaceView.ViewTabs.cs`. The saved layout follows
  (each window's `Views` and `Widgets`), so the order and placement come back on the next start.
- **Widget categories.** Every widget definition has a `WidgetCategory` (`LT.Contracts/Enums`):
  `Ibkr` for widgets backed by Interactive Brokers (all of them so far) and `Tools` for widgets
  that work on their own, without a connection (Notes). Menu → Widgets has one section per
  category, in the enum's order, each under a disabled header item ("Interactive Brokers",
  "Tools", `WidgetMenuHelpers.GetCategoryTitle`) and keeping its own separators
  (`WidgetMenuHelpers.Group` splits `WidgetsMenuItem.Order`). A `Tools` widget derives from
  `ViewModelBase`, not `ConnectedViewModelBase`, and needs nothing from IB.
  Every widget frame (center tile, side, bottom, floating, fullscreen — all one `WidgetFrame`)
  has a 12 px wedge in its top-left corner in its category's color (`WidgetCategoryBrushes`, the
  one mapping: Ibkr indigo `#5C6BC0`, Tools muted gold `#B8973A`, chosen away from buy / sell
  green and red, the fullscreen amber and the link group dots); hovering it names the source.
  The wedge (`Workspace/CornerMarker`) takes the frame's top-left corner radius, so it is rounded
  in the card look and square when flush or docked.
- **Spawning.** Menu → Widgets → any widget always opens a **new instance**
  (`WorkspaceViewModel.Spawn`) where the last one of its kind was docked (the widget's default
  dock at first; the shown view for the center) and makes it current with keyboard focus. Every
  instance has its own view model (widget view models are transient, created by
  `WidgetViewModelFactory`), its own id (`Orders`, then `Orders-1a2b3c4d`, …) and its own saved
  placement and settings. Closing one discards it and disposes its view model; the others stay.
  Ctrl+1 … Ctrl+9 and Ctrl+Alt+key don't spawn: they show an open instance of the widget
  (`WorkspaceViewModel.FindShown`: the current widget if it is one, else one on screen, else one
  in another view, which is then shown) and spawn one only when none is open; Ctrl+1 … Ctrl+9
  collapse it when it is the expanded side widget. Ctrl+7 (Chart) is the exception
  (`WorkspaceHelpers.HotkeySpawns`): it always opens a new chart in the current view, like the
  menu.
- **Charts**: each one opens in the current view. Each chart has its own timeframe, link group and contract, all saved per
  chart. New charts start at the last chart's timeframe (5m on first run). **Changing the
  timeframe:** click the timeframe beside the chart's header title (e.g. "5m"); a menu lists 1m,
  5m, 15m, 1h, 1D and 1W with the current one checked, and picking one reloads the bars
  (historical + real-time) at that size. **Changing the period:** the button after the
  timeframe (e.g. "Auto") picks the span of bars, ending now, in two groups: rolling (1 day,
  7 days, 1/3/6 months, 1/5 years back from now) and calendar (Today, This week, This month,
  This year — from midnight of the current day, Monday, the 1st, 1 January in the display time
  zone, the zone of the chart's time axis; a zone change reloads them). Auto is the timeframe's
  own span (the behaviour before periods; older layouts load as Auto). Logic in
  `ChartPeriodHelpers`; `ChartManager` asks IB for a covering duration and drops older bars.
  Small bars have a longest period (1m 7 days, 5m 1 month, 15m 3 months, 1h 1 year): a longer
  one is cut to it and the status line says "… bars go back … at most"; a period with no bars
  yet (e.g. Today before the session) says "no bars since …". A chosen period is shown whole:
  the chart (`CandlestickChart.FitBars`, bound to `ChartViewModel.FitsPeriod`) zooms out until
  every bar of the new series fits (also on double-click; the wheel can zoom in from there), while
  Auto keeps the latest bars at the default zoom. Period is saved per chart, new charts start at
  the last chart's period, and it is part of chart templates. A load superseded by another
  timeframe, period or contract never shows its bars or its error. There is no Reload button;
  F5 reloads.
- **Pre- and post-market bars:** per chart. On an intraday chart the header's **ETH / RTH**
  button after the period (hidden on daily and weekly, which are always regular hours) switches
  that chart between pre- and post-market bars (ETH, the default) and regular hours only (RTH):
  `ChartViewModel.UseRegularTradingHours` (IB `useRTH`) for historical and real-time bars, and
  only that chart reloads. Saved with the chart (`WidgetPlacementEntity.ChartOutsideRegularHours`;
  null, as in layouts saved before it, is on); new charts start on. It used to be an app-wide
  setting (Settings → Chart); settings files that still carry `ChartOutsideRegularHours` load
  and the value is ignored. With pre- and post-market bars on an intraday chart, bars outside the
  regular session get a lighter background band (all panes) and hollow candles. The regular
  session is the contract's `liquidHours` in its `timeZoneId` from contract details
  (`IChartManager.GetRegularHoursAsync`, cached `CacheConstants.TradingHoursTtl`, fetched once per
  contract per chart while connected). IB lists only a few days around today, so
  `TradingHoursHelpers.Parse` keeps those days as listed (holidays, early closes) and gives every
  other day the most common listed session (Monday–Friday, and a weekend day only when listed
  open); a bar overlapping a session (e.g. the 09:00 hourly bar) counts as regular. IB's old zone
  names (`US/Eastern`) map to IANA ids through `IbTimeZoneConstants.Aliases`. No usable hours
  (or not connected): every bar is drawn as regular. Daily and weekly bars are never marked.
- **Picking a chart's ticker:** click the chart's header title (`ChartTitleView`, hosted in the
  frame header via `WidgetViewFactory.CreateTitle`). It turns into a ticker box: typing lists
  IB's listings of the symbol to pick from, Enter takes the highlighted one or the typed ticker's
  first listing (`IContractManager.ResolveTickerAsync`); an empty box (or the chevron) lists the
  last 10 tickers (`SettingsEntity.RecentContracts`, shared by all widgets). Esc or clicking away
  cancels.
- **Click-to-edit ticker titles** (chart, option chain, news) are one control, `Controls/TickerTitle`
  in `LT.Widgets.Core`. Its view model implements `ITickerTitled` (`Title` plus a
  `TickerTitleEditor`, which holds the editing state, trims and upper-cases typed text, ignores a
  blank one and hands the ticker or picked listing to the widget's own open path).
  `CommitOnLostFocus` makes clicking away open the typed ticker instead of cancelling.
- **Ticker search boxes** (chart and option chain titles, watchlist) are one control,
  `Controls/ContractSearchBox`.
  A typed symbol is looked up as stock / ETF, index and crypto at once
  (`IContractManager.SearchSymbolAsync`, `MarketDataConstants.SymbolSearchTypes`), US-dollar
  listings first. Futures and forex are not searched (every contract month / currency pairs).
  A ticker picked this way goes to the chart's link group.
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
  right. Positions, Orders, Executions, News, Account performance, Day trade performance, Depth,
  Notes and Calendar are closed.
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
  (Load older pages back). The header title ("News · AAPL") is a `TickerTitle`: a ticker typed
  there (Enter or clicking away) becomes the contract, moves the link group along and switches
  All news back to Linked; Esc cancels. *All news* subscribes to the broad tape of each selected provider that
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
- **Depth** (`LT.Widgets.Depth`, right side, closed on first run, Ctrl+Alt+K): the link group's
  contract's order book as a ladder — asks stacked upwards above the spread line (best ask next
  to it), bids downwards below it; rows that don't fit are cut off at the far end, so the inside
  of the book stays in sight. Columns: price (bids green, asks red, best row bold), size, running
  total from the best row out, and the exchange or market maker (SMART / Level 2 depth). A bar
  behind each row shows its size against the largest row on either side. The spread line shows
  spread and mid, and under them a bar split by the size resting on all shown bid rows (left,
  green) against all shown ask rows (right, red), each end labelled with its total and share
  (`OrderBookHelpers.GetSideTotals`; rows past the requested depth aren't counted). The SMART toggle (on by default) switches between SMART depth and the listing
  exchange's own book; a contract on one exchange (crypto on PAXOS, forex on IDEALPRO) always
  uses that exchange. Prices show as many decimals as the book needs (2–6). Updates are read on
  the thread pool, the latest book is drawn every 100 ms (`MarketDepthConstants.UiFlushInterval`),
  rows are fixed view models refilled in place. IB's refusals (no depth subscription, three
  subscriptions already open) show as the widget's error. Closing the widget cancels its depth
  subscription at once (`IWidgetOpenState`, called by `WorkspaceViewModel.Close`; `DepthFeed`
  owns the stream), so the slot is free. Several Depth widgets can be open (each on its own link
  group's contract); past IB's three subscriptions, the extra one shows IB's refusal. Data from `IMarketDepthManager`
  ([ibkr-tws-api.md → Market depth](ibkr-tws-api.md#market-depth)).
- **Calendar** (`LT.Widgets.Calendar`, right side, closed on first run, Ctrl+Alt+V): upcoming
  earnings, other corporate events and economic releases of the **whole market**, merged from
  the sources switched on in **Settings → Calendar → Sources** (`CalendarNasdaq`,
  `CalendarWallStreetHorizon` in `GeneralSettingsEntity`): **Nasdaq** (on by default; no setup,
  no IB connection — [architecture.md → External data: Nasdaq
  calendar](architecture.md#external-data-nasdaq-calendar)) and **IB Wall Street Horizon** (off
  by default: it needs IB's paid subscription, which the live account lacks —
  [ibkr-tws-api.md → Wall Street Horizon](ibkr-tws-api.md#wall-street-horizon-calendar)). Range
  buttons This week / Next week / This month (weeks Monday to Sunday, New York dates) and event
  buttons All / Earnings / Economic / Other events (corporate events but earnings: ex-dividend
  dates, splits, conferences, meetings), both saved per widget (`WidgetPlacementDto.CalendarRange`
  / `CalendarFilter`, null = this week, all; `CalendarEventFilter.Economic` = 3 was appended).
  **Sources…** opens Settings on that page (`SettingsPageNames.Calendar`). One list: a header per
  day ("Thu 29 Oct · Tomorrow", its event count, a warning when WSH returned its 100-per-request
  maximum), then its events — the day's economic releases first by time ("All day" first), then
  corporate events before open / during market / after close / without a time. Each row: time
  badge (Before open / After close, a release's clock time as Nasdaq gives it, or WSH's own
  text), symbol (none for a release), company or release, detail (EPS estimate, dividend amount
  and payment date, split ratio, a release's actual / consensus / previous) and the type or a
  release's country; the tooltip repeats it all and names the source ("from Nasdaq"). An event
  both sources report (same day, kind and symbol) shows once, the first source's (WSH) with
  blanks filled from the other (`CalendarHelpers.Merge`). Clicking an event looks the symbol up
  (`IContractManager.SearchSymbolAsync`, the listing with WSH's conId if it gave one; needs IB) and
  sends it to the link group; headers and releases can't link anything. It never shows a bare
  empty list: a source that failed while others answered is named in a warning line under the
  status ("IB Wall Street Horizon: This IB account has no Wall Street Horizon subscription …",
  "Nasdaq: … (HTTP 403)"); when none answered a notice replaces the list — no WSH subscription,
  an unreadable answer, every source off ("No calendar source is on", where to turn one on) or
  each source's failure; an empty range says "No earnings dates / economic releases / corporate
  events / events between …". The status line counts events, says when they were loaded and from
  which sources. It loads on open whether or not IB is connected, again on connect / disconnect
  (WSH's availability changes; the other sources answer from memory) and when the sources change
  in Settings (`ICalendarManager.SourcesChanged`). Switching range or filter cancels the load
  running; Refresh and F5 ask every source again (otherwise answers of the last 30 minutes come
  from memory, per source). Data from `ICalendarManager`, which merges every `ICalendarSource`.
- **Notes** (`LT.Widgets.Notes`, a Tools widget, right side, closed on first run, Ctrl+Alt+S): a
  free-text pad, one wrapping multi-line text box, no Save button. Every Notes widget has a note
  of its own, its id saved in the layout (`WidgetPlacementDto.NoteId`), so it shows its text
  again after a restart. Notes are never deleted: a new Notes widget opens on the most recently
  edited saved note that no open Notes widget, in any window, shows (`INotesManager.Open`,
  `NotesHelpers.PickUnshown`; the manager counts the notes shown), and starts a new, empty note
  only when every saved note is on screen — so ✕ and opening Notes again brings the text back.
  A restored widget first opens on such a note and then switches to its own when its placement
  loads, freeing the first one. A duplicated view's
  copy keeps the id, so both show the same note and follow each other's edits
  (`INotesManager.Changed`). `NotesManager` (singleton) keeps every note in memory and writes
  them all to `notes.json` in the app-data folder (`NotesRepository`, through a temp file) once
  typing has paused for `NotesConstants.SaveDebounce` (1 s, a `TimeProvider` timer), right away
  when a widget closes or its window is discarded (`Flush`), and when the app exits (the host
  disposes the manager). An emptied note is removed from the file; a closed widget's note stays
  in it for the next Notes widget. A failed write goes to Messages and is retried with
  the next change; an unreadable file is copied to `notes.json.corrupt` before it is replaced.
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
  bounds; layouts saved before windows have none; the main window's position, state and screen
  are `WindowX` / `WindowY` / `WindowState` / `WindowScreen`, an extra window's `State` /
  `Screen`, a floating widget's `FloatState` / `FloatScreen`, all null in older layouts): `Widgets` (id, kind, dock, floating bounds, chart settings of each open
  instance), `Views` (id, name and split tree with sizes of each view) with `SelectedViewId`, the
  expanded widget per side and side widths. Layouts from before views have only `Center` (one
  split tree): it becomes the first view, "Main", on load and is saved as null afterwards
  (`CenterViewHelpers.Normalize`, which also keeps each center widget in exactly one view).

**Adding a widget** (in its own project, like `LT.Widgets.Watchlist`): a `WorkspaceTool` value
(at the end); a `src/Widgets/LT.Widgets.<Widget>` project referencing `LT.Widgets.Core`, added to the
solution's `Widgets` folder and referenced by `LT.App`; an `IWidgetDefinition` there (category, title, icon, focus key, view model type,
view and optional header title) listed in `WidgetCatalog.Definitions` — that also registers its
view model in DI; a default dock in `WorkspaceHelpers.GetDefaultDock`, and a menu entry in
`WidgetsMenuItem.Order` (the Widgets menu of every window; it lands in its category's section). Per-instance settings: implement `IWidgetState` on the view model.

`WidgetCatalog`, `WidgetViewFactory` and `WidgetViewModelFactory` only read the definitions; a
widget's header title is its definition's `CreateTitle()` (`WidgetTitles.Bound` for plain text bound
to the view model).

ViewModels are resolved from DI. They have no interfaces, so the LSCore convention scan skips them —
register each one as transient in `Program.cs` (`builder.Services.AddTransient<FooViewModel>()`);
widget definitions' view models are registered from `WidgetCatalog.Definitions`.
Row VMs (`WatchlistRowViewModel`, `PositionRowViewModel`, …) are constructed by their parent
instead.

### Chart templates

Right-clicking a chart (`ChartView`) → **Templates…** opens its templates at the pointer: named
chart setups — timeframe, period, indicators (type, inputs, visibility, plot colours) and right margin. Click a
template's name to apply it to that chart; the star makes it the **default**, applied to every new
chart (restored charts keep their saved setup); the pencil renames it in place; the export icon
writes it to a JSON file; ✕ deletes it after an inline confirmation. "Save this chart as…" saves
the chart's current setup (a name already taken asks "Replace …?" first; replacing keeps its id
and default). Import… adds the templates of a file (a taken name gets " (2)", " (3)" …; the whole
file is refused, naming the template, when anything in it is invalid); Export all… writes every
template to one file. Results and errors show at the bottom of the menu, which reopens (centred
on the chart) after a file dialog. The same right-click menu's **Apply template** submenu lists
the templates (the default starred) to apply one in a click.

**Follow and save on change.** A chart remembers the template it *uses*: the one last applied to
it, saved from it, or the default it started with (`WidgetPlacementDto.ChartTemplateId`, saved
with the layout). The switch under the list (`ChartFollowTemplate`, also saved) ties the chart to
that template: its timeframe / period / indicator / margin edits are written to the template
`ChartTemplateConstants.FollowSaveDebounce` (400 ms) after they stop — only the settings the
template holds (`ChartTemplateHelpers.Merge`) — and every save of the template, from any chart,
is applied to each chart following it. Charts that don't follow are never touched. Without a
template the switch is disabled. Turning it on while the chart differs from the template
(`ChartTemplateHelpers.Matches`: the held settings, indicators by type, inputs, visibility and
colours, not instance ids) asks in place — "outdated version of template …; following it first
updates the chart" — Cancel leaves everything as it was, *Update and follow* applies the latest
version and then follows. A restored follower takes the template's latest version; deleting the
template stops following.

- No feedback loop: `IChartTemplateManager.TemplateSaved` (raised by `Save` and `UpdateSettings`)
  reaches every chart; applying it runs under `_applyingTemplate`, so it is not an edit and is not
  saved back, and a save the chart already matches (its own included) is ignored. A save arriving
  while the chart's own edit is still debounced wins (the edit is dropped). Applying another
  template, saving a new one, closing the chart or turning following off saves a pending edit
  first.
- `IChartTemplateManager` (`ChartTemplateManager`, a singleton for that event) keeps them in
  `SettingsEntity.ChartTemplates` with `DefaultChartTemplateId`; validation messages are
  `ChartTemplateValidationCodes`, limits `ChartTemplateConstants`, shared checks
  `ChartTemplateHelpers.GetNameError` / `GetSettingsError`. The view picks the files (Avalonia
  storage provider) and hands their text to `ChartViewModel.ImportTemplates` / takes
  `ExportTemplates`; at most `MaxFileLength` (2 MB) is read.
- **Format, versioned for later settings.** A template (`ChartTemplateEntity`) has a `Version`
  (`ChartTemplateConstants.CurrentVersion`, 2) and its `Settings`, every one optional: a setting
  a template doesn't hold (saved before it existed) leaves the chart's own when applied. Version
  2 added the period: a version 1 template (stored or imported) that holds a timeframe is read as
  holding period Auto (`ChartTemplateHelpers.ToDto`), what its charts showed, so charts following
  it share their period too. An
  exported file is `{ "format": "LimitlessTrading.ChartTemplates", "version": 2, "templates":
  [...] }` in camelCase with timeframes by name; reading accepts any property case, enum numbers,
  comments and trailing commas, and ignores fields it doesn't know, so files from newer versions
  import with what this one understands (and are stored as the current version). Adding a
  setting: an optional field in `ChartTemplateSettingsEntity` / `Dto` and both mappers, captured in
  `ChartViewModel.GetTemplateSettings`, applied in `ApplyTemplateSettings`, checked in
  `GetSettingsError`; raise `CurrentVersion` only when an existing field changes meaning.
- Applying gives the chart its own copies of the indicators (new instance ids). Indicators whose
  script isn't on this computer stay in the legend as missing, as in a layout.
- Not in templates: the contract, link group and regular / extended hours (saved per chart, set
  from the chart's header).

### Watchlists

The watchlist widget shows one of several named watchlists. Its header (`WatchlistTitleView`) is
the shown watchlist's name; clicking it opens the list of all of them, with symbol counts. Each row
is picked by clicking its name, renamed in place with the pencil (Enter or clicking away saves,
Esc cancels) or deleted with ✕ after an inline confirmation (disabled when only one is left); the
last row, "+ New watchlist", adds "Watchlist N" and opens its name for editing. Rows are
`WatchlistItemViewModel`s holding their own rename / confirm state; the list lives in a popup, so
row buttons bind to `Owner` (the widget's view model) commands. Adding and removing symbols works
on the shown watchlist. **Every watchlist widget shows its own watchlist**: picking one in a
widget changes only that widget; it is saved in its placement (`WidgetPlacementEntity.WatchlistId`;
older layouts and a deleted list fall back to the active one, `WatchlistHelpers.ResolveShownId`).
`IWatchlistManager` (a singleton) takes the watchlist id for items and pins; its active watchlist
(`ActiveWatchlistId`) is only the one picked last, where a new watchlist widget opens. Every change
raises `IWatchlistManager.Changed`, so widgets showing the same list follow each other (added and
removed symbols, pins, renames) and one whose list was deleted elsewhere shows the active one.
`IWatchlistManager` keeps them in `SettingsEntity.Watchlists`; the single list of older settings becomes "Main" on first load
(`WatchlistHelpers.EnsureWatchlists`). A fresh install (no settings file) starts with a "US" list
of SPY, MSFT, NVDA and TSLA (`WatchlistHelpers.CreateFirstRunSettings`).

The **Mkt Cap** column is the SEC's shares outstanding × the row's last price (previous close
before the first trade), recomputed on every UI flush and sortable by its numeric value
(`MarketCapValue`). Only US stocks (STK in USD) get one; anything else, and stocks the SEC has no
usable count for (ETFs, foreign filers), show "—". Starting a row's quote also starts its
`ISharesOutstandingManager` lookup (cached on disk, so normally instant); the result is posted to
the row on the UI thread. The built-in columns are fixed: nothing about them is persisted.

**Custom columns.** Each watchlist can have up to `WatchlistConstants.MaxCustomColumns` (10)
columns of its own, added, renamed and deleted in the header popup's "Columns" section (same
pencil / ✕ / inline confirmation as watchlists; "+ Add column" adds "Column N" and opens its
name). They show after the built-in ones; a cell holds free text or a number (at most
`CellValueMaxLength`, 200, trimmed; blank clears it). Edit a cell by clicking it on the selected
row (or F2): Enter or leaving the cell saves, Esc cancels. They are kept per watchlist in
`WatchlistEntity.Columns` (`WatchlistColumnEntity`: id, name, non-empty `Cells` by conId +
exchange, like pins; older settings have none) through `IWatchlistManager.GetColumns` /
`SaveColumn` / `DeleteColumn` / `SetCell`; removing an item drops its values, and every change
raises `Changed`, so other widgets on the list follow. The grid columns are built by the view
(`WatchlistView.RebuildCustomColumns`, tagged with the column id) whenever
`WatchlistViewModel.CustomColumns` is replaced (only when columns are added, renamed or removed);
cells bind by index into the row's `Cells` (`WatchlistCellViewModel`: saved `Text`, and
`EditText` for the edit box, saved on the grid's `CellEditEnded` commit). Only these columns are
editable: the grid is not read-only, every built-in column is.

**Pinning and sorting.** Right-click a row > "Pin to top" (or "Unpin"). Pinned rows stay on top,
with a small pin before the symbol, in the order they were pinned, whatever column is sorted;
only the rows below follow the sort. Pins are kept per watchlist in `WatchlistEntity.Pinned`
(conId + exchange, in pin order; settings from before pinning have none) through
`IWatchlistManager.Pin` / `Unpin` / `GetPinned`; removing a row drops its pin. The DataGrid's own
sort can't hold a group in place, so the view handles its `Sorting` event and the view model
owns the sort (`SortColumn`, `SortDirection`, not persisted): a new column sorts ascending, the
sorted one flips. Every sortable column sorts by a number (`LastValue`, `ChangePercentValue`,
`BidValue` for Bid / Ask, `VolumeValue`, `MarketCapValue`), rows without one yet last in either
direction, ties in saved order; Symbol sorts ignoring case. A custom column sorts as
`WatchlistSortColumn.Custom` plus its id (`SortCustomColumnId`): values that read as numbers
(`WatchlistSortHelpers.TryParseNumber`: invariant culture, optional thousands commas such as
"1,234.5", a "+" or a trailing "%"; "1,5" is text) by value and before text, text ignoring case,
blank cells last either way (`GetCustomSortKey`); typing a value re-sorts at once, deleting the
sorted column goes back to the saved order. `WatchlistSortHelpers.Order` gives
the order and `CollectionHelpers.ApplyOrder` moves only the rows that have to (remove + insert:
the DataGrid ignores Move), keeping the selection without re-publishing it to the link group.
The order is re-applied on a header click, pin / unpin and add; while sorted by a quote column,
ticks mark it stale and it re-sorts at most once per `WatchlistResortInterval` (1 s). The view
sets the header's sort arrow itself, as the grid only draws it for its own sort.

**Import / export.** The header popup's "Export <name>…" saves the shown watchlist through the
storage provider's save picker as `{ "format": "LimitlessTrading.Watchlist", "version": 1,
"name", "columns": [names], "items": [{ "contract": {...}, "pin": n, "values": { column: value
} }] }` (camelCase, items in saved order, `IWatchlistManager.Export`, `WatchlistFileHelpers`).
"Import watchlist…" reads a file (at most `WatchlistConstants.MaxFileLength`, 1 MB) and always
creates a new watchlist that the widget then shows (and that becomes active); a taken name gets
" (2)" …. `IWatchlistManager.ImportAsync` reads JSON (content starting with `{`) as an export:
any property case, comments, trailing commas and unknown fields are accepted, the whole file is
checked first (`ImportWatchlistRequestValidator`: our format, 1–200 items each with a conId,
valid distinct column names, values not too long) and the items, pins, columns and values are
added as they were, no IB request needed. Anything else is a plain list of symbols: one per line
or separated by commas, semicolons or tabs, quotes, "#" comment lines and a leading
"Symbol" / "Ticker" header dropped (`WatchlistFileHelpers.ParseSymbols`); the list is checked
(`ImportWatchlistSymbolsRequestValidator`: 1–200 entries that look like tickers) before any is
looked up, then each symbol is resolved to its first listing through
`IContractManager.SearchSymbolAsync` **one at a time**, so the lookups queue behind
`RequestThrottle` (and the contract cache) instead of bursting. Symbols IB doesn't know are left
out and named in the widget's status line; none found adds nothing. A symbol list needs the
connection (the widget says so instead of starting); errors (not a watchlist file, too big, …)
are validation messages (`WVC_015`–`WVC_021`) in the widget's error line, and nothing is added.

### Pinned side widgets

A widget docked on a side has a pin in its header. Pinned (the default) it stays expanded until
you click its strip icon; unpinned (tilted pin) it collapses as soon as you click or move
focus anywhere outside its panel — another widget, the toolbar, a floating window. Its own strip
icon, splitter and dropdowns don't count. `WidgetItem.IsPinned`, saved in the layout;
`WorkspaceView` watches clicks and focus on the window (tunnelling handlers).

**Esc** in an expanded side or bottom widget hands keyboard focus back to the center widget
focused last (or the first center widget, if none was), which becomes current; an unpinned
widget also collapses, a pinned one stays open. Only an Esc nothing inside the widget handled
counts — an open dropdown, a ticker box or a rename box keeps its own Esc. An always-shown
ticker box (the watchlist's, `ContractSearchBox.PassesIdleEscape`) keeps it only while it has
text or an open dropdown: the first Esc clears it, the next one reaches the workspace
(`WorkspaceView.OnDockedKeyDown`, bubbling, → `WorkspaceViewModel.LeaveDocked`; the target is
`WorkspaceHelpers.GetFocusReturnId`).

### Fullscreen

The fullscreen button next to a widget's ✕, or Ctrl+Shift+F, shows the widget over the whole
window below its title bar (status bar included; the title bar, part of `AppWindow`'s template,
stays), framed in orange; the same button or shortcut restores it.
One widget at a time. `WorkspaceViewModel.FullscreenWidget` holds it; `WorkspaceView` moves its frame into the
window's fullscreen layer (`IFullscreenHost`, implemented by `MainWindow`) and back, so the widget
keeps its state. A floating widget maximizes its own window instead. Moving or closing a
fullscreen widget restores it first; its header doesn't start a drag while fullscreen.

### Link groups

Watchlist, Positions, Orders, Executions, Chart, Option chain, Order ticket, News, Depth and Calendar can be linked:
the chain icon left of the frame's close button opens a menu of ten color groups (Red, Green, Blue, Yellow, Orange, Purple, Cyan,
Pink, Lime, White) or Not linked, and is tinted
with the current one. Widgets in the same group show the same contract:

- Clicking a watchlist, positions, orders or executions row, or picking a chart's ticker, sends
  that contract to the group; charts and option chains in the group switch to it. Remembered
  (stale) positions, orders and fills send nothing. Orders and Executions rebuild their list on
  every refresh and keep the same row selected (by permId / execId) without sending it again, so
  a refresh never pulls the group back. An execution's contract goes out without the fill's
  venue, so linked widgets ask through SMART.
- Positions, Orders, Executions and Calendar only send: a contract from the group doesn't filter or move
  their lists.
- Joining a group shows the group's contract; joining an empty group gives it the widget's own.
- New widgets start in Red, so out of the box they all follow each other. "Not linked" keeps a
  widget on its own contract.

The option chain's underlying comes from its group, and the frame header shows that ticker
(`TickerTitle`, bound to `OptionChainViewModel.Title`). Clicking it types another underlying, as
on a chart: Enter or clicking away opens the typed ticker (upper-cased, blank ignored), Esc
cancels; the ticker goes through the same path as one from the group and moves the group along.
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
**ask** cell buys and on a **bid** cell sells — calls and puts alike — the widget's one-click
quantity of contracts, market, DAY, on the account selected in `AccountStore` (the one the Order
ticket starts on). The quantity is the "Qty" box at the right of the chain's status line, in sight
while clicking: 1 by default, whole contracts from 1 to 100 (`OrderConstants.QuickOrderQuantityMax`,
a fat-finger guard that applies to one-click orders only, not the Order ticket). An empty or
out-of-range box sends nothing and says so in the error line (`OrderValidationCodes.OVC_012`);
the box has no maximum on purpose, so a typo above the cap is refused, never clamped to 100. Each
chain instance saves its quantity in its placement (`WidgetPlacementEntity.OptionQuickOrderQuantity`,
null = default; an invalid saved value loads as the default). The order is sent
immediately through `IOrderManager.PlaceOrderAsync` (same validation as the ticket); there is no
review step. Only bid / ask cells are clickable: they show a hand cursor and a translucent green
(buy) / red (sell) tint on hover over the ITM shade; last, volume and strike cells don't react.
Hovering an ask cell shows the estimated cost of the click, quantity × ask × the contract's
multiplier (`OptionChainHelpers.GetQuickOrderCost`, multiplier from `ContractHelpers.GetMultiplier`),
e.g. "≈ 2 × 1.35 × 100 = 270.00 USD at the ask" — an estimate, the order goes at market. It
follows the quote and the Qty box live, and shows while the cell is disabled
(`ToolTip.ShowOnDisabled`) to say "No price", "multiplier unknown" or why the quantity is invalid.
A cell is disabled while disconnected, when its side has no contract or that column has no price,
and while its own order is in flight. Repeat clicks on the same cell within 500 ms
(`OrderConstants.QuickOrderRepeatWindow`) are dropped, so a double click sends one order. The
chain's status line shows "Order n sent: Buy 5 …" (with the quantity) for a few seconds and the
fill; a failed send or a rejection shows in its error line, and everything is logged to Messages
(Orders). Mapping and
guard logic: `OptionChainHelpers.GetQuickOrderAction` / `BuildQuickOrder` / `CanQuickTrade` /
`IsRepeatClick` / `IsValidQuickOrderQuantity`.

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
- **Show deposits & transfers** (check box, off by default, kept per widget in its placement as
  `PerformanceShowCashMovements`) marks the Flex cash movements (`IAccountPerformanceManager.GetCashMovements`)
  with badges: deposit a green +, withdrawal an orange −, transfer in / out a purple arrow down /
  up (`ChartDrawing.DrawCashMovementBadge`, `CashMovementIcon` for the calendar and the legend).
  On both charts they stack along the bottom of the day they reach (the first day with data on or
  after the movement, `AccountPerformanceHelpers.AssignCashMovements`) and the hover readout lists
  them; in the calendar they sit in the cell's corner on their own date, the tooltip with amount,
  original currency and IB's description. A hint under the box (`FlexStatusHelpers.GetCashMovementsHint`)
  says what to add to the Flex query when Cash Transactions or Transfers are missing, that they
  are being imported, or that none were found. Display only: the P&L math already leaves
  deposits and transfers out (`AccountPerformanceHelpers.MergeFlexDays`).

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

When every value matters (a chart's live trade prices, where a dropped tick would lose a high or
low), queue them instead of keeping the newest: the thread pool enqueues and posts once per
burst (an `Interlocked` flag), the UI thread drains the queue — see `ChartLiveBars`.

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
| F5 | Refresh chart, account, positions, orders, executions, day trade performance, calendar |
| Ctrl+, | Settings |
| Ctrl+1 … Ctrl+9 | Show an open Watchlist, Positions, Orders, Executions, Account, Messages, Chart, Option chain, Order ticket, opening one if none is (collapses an expanded side widget); Ctrl+7 always opens a new chart in the current view |
| Ctrl+Alt+W / P / O / E / A / M / C / B / N / R / J / K / S / V | Show and focus an open Watchlist, Positions, Orders, Executions, Account, Messages, Option chain, Order ticket, News, Account performance, Day trade performance, Depth, Notes, Calendar, opening one if none is (works from floating windows too) |
| Esc (side or bottom widget) | Back to the last focused center widget; an unpinned widget collapses |
| Ctrl+Tab / Ctrl+Shift+Tab | Next / previous center view |
| Alt+A (chart) | Add a price alert at the price under the pointer, or at the last price |
| Esc (chart) | Disarm the toolbox's armed tool (e.g. placing an alert) |
| Ctrl+Shift+F | Fullscreen the current widget (the one last clicked or typed in, framed in light blue), or restore it |
| Ctrl+Shift+N | New empty workspace window |

Ctrl+Shift+*key* combos act on a widget.

Menu → Widgets lists the widgets without shortcut labels; a widget's shortcut shows in its dock
icon's tooltip ("Chart (Ctrl+7)", "Watchlist (Ctrl+Alt+W)", `WidgetCatalog.GetShortcut`), and
Ctrl+Shift+F in the fullscreen button's.

The window handles them in a tunnelling `KeyDown` handler (`MainWindow.axaml.cs`, and
`WorkspaceWindow.axaml.cs` for its own workspace — everything but Ctrl+,), so they work
while a text box has focus. On macOS, Cmd works in place of Ctrl.

## Charts

`Widgets/Chart/CandlestickChart` is a custom `Control` drawn with `DrawingContext` (no chart
library): candles, volume, price grid, last-price line, crosshair + OHLCV readout. Wheel zooms,
drag pans, double-click resets (zoom and pan, not the right margin); the right button doesn't
pan — releasing it opens the chart's context menu (Add alert at …, Remove alert, Remove triggered
alerts, Manage indicators…, Apply template, Templates…, Show toolbox). The account performance widget's `EquityCurveChart` and
`DailyPnlBarChart` are drawn the same way (same palette, `ChartDrawing`).

**Toolbox.** A thin bar between the widget header (ticker, timeframe, period, RTH / ETH) and
the plot holds the chart's tools (`ChartViewModel.Tools`, one `ChartToolItem` per `ChartTool`;
a new tool is a value appended to the enum plus an entry in `CreateTools`). Each chart shows or
hides it — right-click → *Show toolbox* (a check item) or the bar's ✕ — saved per chart in the
layout (`ChartShowToolbox`; null in older layouts is shown, the default). It is not part of a
chart template. A tool's button arms it (`ActiveTool`, the button highlighted) for the next click
on the chart; the button again, Esc (while the chart widget has the keyboard) or hiding the bar
disarm it. The only tool so far is **Alert**: while armed, a dashed violet preview line follows
the pointer's price on the price chart (rounded as the axis shows it) with its level on the
axis and the direction it would get (▲ / ▼, from the last close), the cursor is a cross, and a
left click there adds the alert (`ChartViewModel.PlaceAlert` → the same create flow as *Add
alert*) and disarms. The click doesn't pan, drag an alert line or hit a legend; a click off the
price chart does nothing, the right button still opens the menu. Disabled (and disarmed) while
the chart has no bars.

**Indicators and panes.** Right-click → Manage indicators… adds and removes indicators; they draw on the
price chart, on the volume bars or in panes of their own stacked under the price chart, each
pane with its own scale and a legend (hover a row for hide / settings / remove). All panes share
the horizontal viewport and the crosshair. While bars load, a `LoadingSpinner` (shared control in
`LT.Widgets.Core/Controls`: an accent arc sweeping round a faint track, animated only while
visible) sits in the middle of the chart; the error text sits top centre, clear of the legend.
Details: [indicators.md](indicators.md).

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

**Price alerts.** Right-click → *Add alert at <price>* (the price under the pointer, rounded as
the axis shows it; the last price when opened off the price chart), Alt+A while the chart has
the keyboard (a click gives it) or the toolbox's Alert tool (click the price; see *Toolbox*)
adds an alert with no dialog. Its direction is told from the
chart's last close: a level above it fires once the price is at or above it, one at or below it
once the price is at or below it (`PriceAlertHelpers.GetCondition` / `IsCrossed`; a level, not a
pair of prices, so a gap or a move while disconnected fires on the first price seen). Each alert
is a violet line with a tag at the plot's right end (▲ / ▼, its note) and its level on the axis;
dragging the line or tag moves it (the direction is told again, a triggered one is armed again),
✕ on the tag or right-click on the line → *Remove alert* deletes it. An alert fires once: it turns
into a grey dotted "fired" line until removed (one by one, or *Remove triggered alerts*), the
message log gets an entry (source "Alerts") and the **alert popup** opens: a topmost window not
owned by any other (so it shows over every window, focused or minimized) listing every fired
alert not dismissed yet, newest first — symbol, direction and level, the price that fired it, the
note and the time — each with *Dismiss*, and *Dismiss all* when there are several. An alarm
(`SoundHelpers.CreateAlarm`, two high-low beep pairs, about 0.7 s) plays at once and again every
`PriceAlertConstants.AlarmRepeatInterval` (2 s) until the last alert is dismissed; closing the
popup dismisses them all. Alerts off the visible price range are not drawn.

- `IPriceAlertManager` (`PriceAlertManager`, a singleton) holds every alert, saved in
  `price-alerts.json`; `ChartViewModel.Alerts.cs` shows the charted contract's and refreshes on
  `AlertsChanged` (posted to the UI thread). Validation: `CreatePriceAlertRequest` /
  `MovePriceAlertRequest`, messages `PriceAlertValidationCodes`, limits `PriceAlertConstants`
  (200 active alerts, 20 contracts).
- Alerts fire whether or not a chart shows them: `PriceAlertService` (hosted) runs
  `IPriceAlertManager.WatchAsync` while connected, one quote line per contract with an active
  alert through `IMarketDataManager` (so it counts against the line limit, on top of a chart's own
  line in the quote modes). Last trade price, the bid / ask midpoint for forex; delayed quotes
  count. A stream that fails is logged and opened again after `PriceAlertConstants.RetryDelay`
  (1 min); a contract whose alerts have all fired is no longer watched.
- The popup's state is `IPriceAlertAlarmManager` (`PriceAlertAlarmManager`, a singleton fed
  `AlertTriggered` by `PriceAlertService`): the pending alerts and one `TimeProvider` timer for
  the alarm, through `ISoundPlayer.PlayAlarm`. `MainWindow` opens `PriceAlertPopupWindow`
  (`LT.App/Widgets/PriceAlerts`) on its `Changed` (posted to the UI thread), brings it to the
  front on a new alert and closes it once none is pending. Nothing is saved: an alert still in
  the popup at exit doesn't ring again on the next start.
- Not built yet: editing an alert's note (the field is stored and shown), an alerts list, a
  setting to silence the alarm, conditions other than crossing a price.

**Live updates.** After its history loads, a connected chart follows IB's 5-second real-time
bars (`IChartManager.SubscribeRealtimeBarsAsync`, merged with `BarHelpers.Merge`). Settings →
General → Charts → *Chart live updates* (`ChartLiveUpdateMode`, saved in the app settings) can
make it faster:

- **5-second bars** (`RealtimeBars`, the default) — only the 5-second bars.
- **Every trade (quotes)** (`Quotes`) — the 5-second bars plus the quote stream
  (`IChartManager.SubscribeLivePricesAsync` over `IMarketDataManager`, one market data line per
  chart): each last-trade price (midpoint for forex) moves the last bar's close and extends its
  high / low, and a price past the bar's end starts the next intraday bar
  (`BarHelpers.MergePrice`). Prices never add volume, the 5-second bars do. Prices count only while
  5-second bars arrive (`ChartConstants.LivePriceMaxBarAge`), so extended-hours trades don't paint
  bars on an RTH chart; a 5-second bar that ended before the newest price keeps that price as
  the close, and one of the bar before a price-started bar is folded into it.
- **Every trade (quotes), throttled redraw** (`QuotesThrottled`) — the same, but `Bars` is
  assigned at most once per *Chart redraw interval* (250–5000 ms, default 1000;
  `ChartConstants.RedrawIntervalMs*`, `RedrawThrottle` on `TimeProvider`). The interval setting
  is greyed out for the unthrottled modes.
- **Every trade (tick-by-tick)** (`TickByTick`) and its **throttled redraw** twin
  (`TickByTickThrottled`) — the same as the quote modes, but the prices are IB's tick-by-tick
  trades (`Last`; `MidPoint` for forex) stamped with IB's time, so no clock skew at bar
  boundaries. IB allows 5 at a time per account (counted by `IMarketDataManager`); when none is
  left or IB refuses (see [ibkr-tws-api.md](ibkr-tws-api.md#tick-by-tick)) the chart falls back
  to quotes (`ChartLiveUpdateHelpers.GetFallback`, the throttle stays), then to 5-second bars.
  Volume still comes only from the 5-second bars.

`ChartLiveBars` holds the working bars of one load: 5-second bars are added on the UI thread, the
price stream runs on the thread pool and queues prices, which are folded one burst per UI post
(every trade counts, no post per tick). Prices count only while a 5-second bar arrived within
`LivePriceMaxBarAge` on the app's clock. The status line names the price source in use
("Live · 10:31:05 · tick-by-tick", "· quotes", "· 5-second bars only"); every fallback is logged
to the message log.
Saving a different mode (or interval, for the throttled modes) reloads every open chart.

## Styling

- Colours/brushes as resources in `App.axaml`; positive/negative P/L via the `positive` /
  `negative` classes, never hard-coded in a view.
- Widgets sit in a `Border.panel` with a `TextBlock.panelTitle` header.
- A number box in a header or status row uses `NumericUpDown.compact` (24px, centred text, slim
  `CompactButtonSpinner` with flat chevrons). Fluent sizes its spinner buttons inside the
  template, so shrinking `MinHeight`/`Padding` on the control alone only squashes the text.
