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
  (`Widgets/ConnectionBar`, a flyout). The market data item (`streams N/90`) opens a flyout
  listing every line in use — contract, quote / news (and generic ticks such as option IV), who
  opened it and since when — from `IMarketDataManager.GetActiveStreams`, refreshed on
  `StreamsChanged`. Callers pass an owner label (`MarketDataStreamOwners`) with each
  subscription so the list says what fills the limit. Quote subscriptions of one contract share
  a line, so a shared line lists every owner and how many subscriptions read it ("Watchlist ·
  US, Chart (2)"), and the summary counts it for each owner.

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

## Privacy mode (the eye in the title bar)

The eye left of the main window's window buttons hides sensitive values everywhere — account ids,
account values (net liq, cash, buying power, margin), P&L, position quantities, avg cost and market
value, order and execution quantities, commissions, day-trade and account-performance figures and
their charts' value axes and readouts — behind `Sensitive.Mask` ("••••••", always the same
length). Symbols, quotes, prices, chart bars and option chains stay visible. The state is
`PrivacyStore` (a store in `LT.Widgets.Core`, saved as `HideSensitiveValues` in `settings.json`, so
it survives a restart); it pushes into `Sensitive.IsHidden`, the view side every window reads.

Marking a value sensitive (only what is drawn changes; bound values and view models never see the
mask, and everything on screen follows the switch at once):

- a TextBlock → `controls:SensitiveTextBlock` (styled as a TextBlock; empty text stays empty);
- a DataGrid text column → `controls:SensitiveTextColumn`;
- an account picker → `ComboBox Classes="sensitive"` (its items become `SensitiveTextBlock`s);
- a control that draws values itself → pass them through `Sensitive.Apply(text)` in `Render` and
  set `helpers:Sensitive.Redraws="True"` on it.

A text that mixes sensitive and plain parts is masked as a whole; split it when that hides too much.

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
- **Layout export / import.** Menu → Export layout… writes the whole layout to one JSON file
  (another computer, a backup, sharing); Import layout… replaces it, live. The file is the
  settings file's `Layout` section (`LayoutEntity`: every window with bounds, state and screen,
  views, docked and floating widgets with their per-instance settings) wrapped in
  `LayoutFileEntity` with `Format` (`LayoutFileConstants.FileFormat`) and `Version`
  (`CurrentVersion`, raise it when the layout changes meaning; older files must keep importing).
  camelCase, enums as numbers like the settings file, so a widget kind this version doesn't know
  is dropped on load instead of failing the file (`LayoutFileHelpers`). Nothing else of the
  settings goes in it: no connection, Flex token or selected account; watchlists, notes and chart
  templates are data, not layout, and stay as they are (a widget pointing at one that doesn't exist
  falls back as with a deleted one). Export saves the live layout first (as on exit,
  `MainWindow.StoreLayout`) and then `ISettingsManager.ExportLayout`. Import reads the file
  (at most `MaxFileLength`), asks, then `ISettingsManager.ImportLayout` validates it
  (`ImportLayoutRequestValidator`: too big, damaged, not ours, newer version — the current layout
  is untouched and the reason shown) and saves it; `WorkspaceWindows.Replace` discards the extra
  windows with their widgets, replaces the main workspace (`MainChanged`, as on a take-over) and
  opens the file's windows; the main window takes the file's bounds and state. No restart.

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
  timeframe (e.g. "Auto") zooms the chart to a span ending now, in two groups: rolling (1 day,
  7 days, 1/3/6 months, 1/5 years back from now) and calendar (Today, This week, This month,
  This year — from midnight of the current day, Monday, the 1st, 1 January in the display time
  zone, the zone of the chart's time axis). **A period is a zoom preset, not a filter**
  (TradingView-style): the chart always loads at least the timeframe's own span (what Auto loads;
  more when the period needs it) and keeps every bar; the period only fits the view, from its
  start (`ChartViewModel.PeriodStart` → `CandlestickChart.FitStart`) to the latest bar, the older
  bars left of it. Picking a period the loaded bars already reach back to just refits; otherwise
  the chart reloads with enough history. Auto is the latest bars at the default zoom (the
  behaviour before periods; older layouts load as Auto). Logic in `ChartPeriodHelpers`
  (`GetRange`, `GetFirstIndex`). Small bars have a longest period (1m 7 days, 5m 1 month, 15m 3
  months, 1h 1 year): a longer one is cut to it and the status line says "… periods go back … at
  most"; a period with no bars yet (e.g. Today before the session) keeps the default zoom and says
  "no bars since …". The fitted view stays fitted as the plot is resized or bars arrive
  (`ChartViewportHelpers.GetBarSpacing`; a restored chart is laid out narrower first). **Custom:**
  zooming (wheel), panning (drag) or dragging the margin handle leaves the period's view: the chart
  reports it (`CandlestickChart.IsFitted` → `ChartViewModel.IsPeriodFitted`), the header says
  "Custom" and the period menu checks nothing. Picking a period again (the same one too) or
  double-clicking the chart fits the view to it again and the header shows it. Auto is never
  Custom (it is no span). What is saved — per chart, in templates, and for new charts (they start
  at the last chart's period) — is the chosen period, never Custom. **Older history:** scrolling or
  zooming to within half a screen of the oldest loaded bar (`CandlestickChart.OlderBarsNeeded`,
  `ChartViewportHelpers.NeedsOlderBars`) loads the page before it
  (`ChartViewModel.LoadOlderBars`, see [ibkr-tws-api.md](ibkr-tws-api.md#how-ltibkr-applies-the-rules)):
  one page at a time, only while connected, put in front (`BarHelpers.Prepend`, through
  `ChartLiveBars` while live bars stream) with the view staying where it is (its position counts
  from the latest bar), until IB has no more or the chart holds 50,000 bars. Indicators compute
  again over the longer series. A page still on its way when the contract, timeframe or trading
  hours change, the chart reloads or the connection drops is dropped. A load superseded by another
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
  has one (the DJ-* providers share one tape, filtered to the selected ones; chips without one
  are dimmed in that mode). Provider chips choose what is asked for
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
  owns the stream), so the slot is free; so does hiding it (an inactive view, a collapsed panel,
  a minimized window — see [Lifetime of subscriptions](#lifetime-of-subscriptions)), and showing
  it subscribes again. Several Depth widgets can be open (each on its own link
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
  buttons Today / This week / Next week / This month (weeks Monday to Sunday, New York dates;
  `CalendarRange.Today` = 3 was appended) and event
  buttons All / Earnings / Economic / Other events (corporate events but earnings: ex-dividend
  dates, splits, conferences, meetings), both saved per widget (`WidgetPlacementDto.CalendarRange`
  / `CalendarFilter`, null = this week, all; `CalendarEventFilter.Economic` = 3 was appended).
  Opening the widget or switching the range scrolls to now; clicking the checked range again
  scrolls back there.
  **Sources…** opens Settings on that page (`SettingsPageNames.Calendar`). One list: a header per
  day ("Thu 29 Oct · Tomorrow", its event count, a warning when WSH returned its 100-per-request
  maximum), then its events — the day's economic releases first by time ("All day" first), then
  corporate events before open / during market / after close / without a time. Today's header
  is in the accent colour. Events already past are dimmed (and days before today), with a
  "Now" line between the last past event and the next one when the range holds today and
  events lie on both sides; an event counts as past at its clock time (a release), the open
  (before open), the close (during market), 20:00 (after close) or the end of its day
  (anything else), New York time (`CalendarHelpers.GetPassedAt`, times in `CalendarConstants`).
  A `TimeProvider` timer due when the next event passes keeps both current; New York midnight
  loads the range again. Each row: a release's country flag on the left (`CountryFlag` in
  `LT.Widgets.Core/Controls`, drawn shapes rather than emoji, which Windows can't draw; the
  country or currency → code in `CountryFlagHelpers`, the euro area → EU; the country in its
  tooltip), time
  badge (Before open / After close, a release's clock time as Nasdaq gives it, or WSH's own
  text), symbol (none for a release), company or release, then on the right either an earnings
  report's three figure columns — **Est.** (consensus EPS), **Actual** (reported EPS) and
  **Surprise** (green a beat, red a miss; `positive` / `negative` classes) — fixed-width,
  right-aligned, tabular digits, an em dash for a figure the source didn't give, Actual and
  Surprise blank until reported (Nasdaq lists results from the day after the report;
  `CalendarEventDto.EpsEstimate` / `EpsActual` / `EpsSurprisePercent`) — or the detail
  (dividend amount and payment date, split ratio, a release's actual / consensus / previous,
  WSH's earnings text) with the type (or a release's country when no flag is drawn for it).
  Earnings carry no type label. The columns' captions sit in the day's header when the day
  starts with earnings, else over its first earnings row (below the releases). A past row dims
  all but its result: Actual and Surprise stay at full strength. The tooltip repeats it all
  ("EPS $4.89 · est. $4.71 · +3.8%") and names the source ("from Nasdaq"). An event
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
- **Overview** (`LT.Widgets.Overview`, right side, closed on first run, Ctrl+Alt+I): one
  contract in depth. The contract comes from the link group, a ticker typed into the header
  title (a `TickerTitle`, moving the group along) or a ticker dropped on the widget
  (`ITickerDropTarget`, like a chart); it is saved with the widget
  (`WidgetPlacementDto.OverviewContract`) and shown again on restart when its link group has no
  contract yet (a group that has one wins, so linked widgets agree). On top a quote header: IB's
  long name, listing · currency · type, last price with the change against the previous close,
  then bid / ask with sizes, open, previous close, day and 52-week range, volume and 90-day
  average volume — one market data line (`SubscribeQuotesAsync` with generic tick 165, owner
  "Overview"), only while the widget is shown; "Waiting for a quote…", "Not connected",
  "Delayed", "Halted" or IB's refusal ("No quote: …") show next to the price. Under it a
  `TabControl` of `OverviewTabViewModel`s (the tab shown is saved, `OverviewTab`, appended
  values): the widget reads the contract's IB details once per contract
  (`IContractManager.GetDetailsAsync`) and hands contract, details and each flushed quote to
  every tab; a tab loads anything else itself. **Basic info**: Instrument (name, symbol, local
  symbol, type, exchange, currency, contract ID, ISIN and other security ids, market, expiry /
  strike / multiplier for derivatives), Classification (industry, category, subcategory),
  Trading (market state now — "Open · closes 16:00", "Pre-market · opens 09:30", "After hours ·
  until 20:00", "Closed · opens Mon 04:00", refreshed every 30 s — today's regular and extended
  hours, time zone, min tick, contract month, routing exchanges, underlying ID; all in exchange
  time, `OverviewHelpers`) and, for US stocks, Shares (SEC shares outstanding via
  `ISharesOutstandingManager` and the market cap at the live price). Blank fields are left out;
  until the details load only what the contract holds shows, with "Loading details…", "Not
  connected to IB: …" or "IB has no details for …" in the header. Adding a tab: an
  `OverviewTab` value at the end, a subclass of `OverviewTabViewModel` created in
  `OverviewViewModel`'s constructor, and a `DataTemplate` for its view in `OverviewView.axaml`.
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
usable count for (ETFs, foreign filers), show "—". Every row's `ISharesOutstandingManager`
lookup starts on connect (and when the row is added; cached on disk, so normally instant); the
result is posted to the row on the UI thread. The built-in columns can't be renamed or moved,
only hidden.

The **Avg Vol** column is IB's 90-day average daily volume: tick 21 from generic tick 165
(`MarketDataConstants.AverageVolumeGenericTick`), in shares like the Vol column, shown with the
same compact format (`FormatHelpers.Volume`) and sorted by `AverageVolumeValue`. Live rows ask
for 165 only while the column is shown or sorted by (`WatchlistColumnHelpers.GetGenericTicks`):
other generic ticks mean another line, so a watchlist line with 165 is not shared with a chart's
or the order ticket's line of the same contract. Showing, hiding or sorting by it reopens the
streaming rows' lines. Snapshots can't carry generic ticks, so a row keeps the last average it
streamed; rows that never streamed (off screen since connecting) show "—" and sort last.

**Vol/Min** is IB's volume rate: tick 56 (a generic tick, shares per minute, smoothed by IB over
the last minutes) from generic tick 295 (`MarketDataConstants.VolumeRateGenericTick`, which also
brings the unused trade rate, tick 55). **Avg Vol/Min** is the Avg Vol spread evenly over a US
regular session (`MarketDataConstants.RegularSessionMinutes` = 390, used for every listing;
`TickTypeHelpers.GetAverageVolumePerMinute`), so the two compare directly: Vol/Min above
Avg Vol/Min is a stock trading faster than on an average day. Intraday volume is U-shaped, so
the open and close normally run above it and midday below. Both use the compact volume format,
sort by `VolumeRateValue` / `AverageVolumeRateValue` and, like Avg Vol, keep their last value
when a snapshot comes. The lines ask for 165 while Avg Vol or Avg Vol/Min is shown or sorted by
and for 295 while Vol/Min is (`"165,295"` with all of them).

**Only the rows on screen stream.** A watchlist holds up to 200 symbols and IB serves ~100 lines
per account, so the view reports the rows it has on screen (`WatchlistView.ReportShownRows`: the
grid's realized `DataGridRow`s inside its rows presenter, after every layout pass, coalesced;
none while the widget is hidden or detached) to `WatchlistViewModel.SetShownRows`. Those, and
`WatchlistLiveRowBuffer` (5) rows on either side (`WatchlistStreamingHelpers.GetLiveRange`;
pinned rows are on top, so they count when shown), stream through
`IMarketDataManager.SubscribeQuotesAsync`. Before the grid first reports, the first
`WatchlistInitialLiveRows` (30) stream. A row that leaves the range keeps its line for
`WatchlistLiveReleaseDelay` (3 s) so scrolling back and forth doesn't resubscribe. Every other row
gets **snapshots** (`IMarketDataManager.GetQuoteSnapshotAsync`, no line): the snapshot timer
(`WatchlistSnapshotCheckInterval`, 2 s, `RefreshOffScreen`) keeps up to
`WatchlistSnapshotsInFlight` (10) running, picking rows without a quote first, then those whose
last quote is older than `WatchlistSnapshotRefreshInterval` (30 s); IB answers each in ~11 s, so a
200-row list is filled in about 3–4 minutes. Rows keep their last values, so sorting by a quote
column still covers the whole list (off-screen values are up to a few minutes old). A failed
snapshot (no subscription) shows the row's error icon, is logged once and waits
`WatchlistSnapshotErrorRetry` (5 min). **No free line** degrades instead of erroring: the
watchlist leaves `WatchlistReservedLines` (10) of the app's 90 for charts and the option chain,
and a row on screen that finds no line (that reserve, the app's limit or IB's `101 Max number of
tickers`, `MarketDataErrorHelpers.IsNoFreeLine`) shows snapshots, marked with a small "S"
(`IsLineLimited`), tries to stream again after 30 s, and the message log hears it once per
connection.

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

**Managing columns.** The header popup's "Columns" section lists every column, built-in then
custom (`WatchlistViewModel.Columns`, `WatchlistColumnOptionViewModel`): a check box shows or
hides it, an arrow sorts by it (⇅ unsorted, ↑ / ↓ sorted; a click sorts or flips, like a header
click), custom ones also rename (pencil) and delete (✕); "Clear sort" goes back to the saved
order. Right-clicking a column header opens the same as a menu (`WatchlistView.OpenColumnMenu`:
sort low to high / high to low, clear sort, hide column, then every column's show / hide check).
**A hidden column can still be sorted by**: hiding only takes it off the grid
(`DataGridColumn.IsVisible`, set by the view on `ColumnVisibilityChanged`), the order is computed
from the rows as before and keeps following ticks; while the sorted column is hidden a line above
the grid says "Sorted by Vol (hidden) ↓" with a ✕ to clear it (`HiddenSortText`). The last shown
column can't be hidden (`WatchlistColumnHelpers.CanToggle`); every built-in column hidden while
the last shown custom one is deleted (or the widget switches to a list without it) brings Symbol
back (`ResolveHidden`). Both are kept **per widget** in its placement:
`WidgetPlacementEntity.WatchlistHiddenColumns` (built-in, as `WatchlistSortColumn` values),
`WatchlistHiddenCustomColumns` (column ids), `WatchlistSortBy` + `WatchlistSortCustomColumnId` +
`WatchlistSortDescending` (null when unset; older layouts show everything, unsorted). A saved
sort by an unknown or deleted column falls back to the saved order
(`WatchlistSortHelpers.ResolveSort`, then the column check in `LoadColumns`).

**Pinning and sorting.** Right-click a row > "Pin to top" (or "Unpin"). Pinned rows stay on top,
with a small pin before the symbol, in the order they were pinned, whatever column is sorted;
only the rows below follow the sort. Pins are kept per watchlist in `WatchlistEntity.Pinned`
(conId + exchange, in pin order; settings from before pinning have none) through
`IWatchlistManager.Pin` / `Unpin` / `GetPinned`; removing a row drops its pin. The DataGrid's own
sort can't hold a group in place, so the view handles its `Sorting` event and the view model
owns the sort (`SortColumn`, `SortDirection`, saved with the widget, see above): a new column sorts ascending, the
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
The order is re-applied on a header click, pin / unpin and add; while sorted by a quote column
(shown or hidden), ticks mark it stale (`FlushQuotes`, the UI flush timer; `ResortIfStale`, the
resort timer) and it re-sorts at most once per `WatchlistResortInterval` (1 s). The view
sets the header's sort arrow itself, as the grid only draws it for its own sort.

**Import / export.** The header popup's "Export <name>…" saves the shown watchlist through the
storage provider's save picker as `{ "format": "LimitlessTrading.Watchlist", "version": 1,
"name", "columns": [names], "items": [{ "contract": {...}, "pin": n, "values": { column: value
} }] }` (camelCase, items in saved order, `IWatchlistManager.Export`, `WatchlistFileHelpers`).
"Import watchlist…" reads a file (at most `WatchlistConstants.MaxFileLength`, 1 MB) and always
creates a new watchlist that the widget then shows (and that becomes active); a taken name gets
" (2)" …. `IWatchlistManager.ImportAsync` reads JSON (content starting with `{`) as an export:
any property case, comments, trailing commas and unknown fields are accepted, the whole file is
checked first (`ImportWatchlistRequestValidator`: our format, 1–200 items each with a conId or
a ticker-like symbol, valid distinct column names, values not too long) and the items, pins,
columns and values are added as they were, no IB request needed. An item without a conId (a
hand-made or generated file) is looked up by its fields one at a time
(`IContractManager.FindAsync`: no security type searches the symbol like a ticker box, else one
cached search with every given field, stocks via SMART) and `ContractHelpers.PickMatch` picks
the listing: every given field must match (expiry by prefix, right by first letter), then no
type prefers stock, no currency USD, a given exchange the listing on it or primary there; more
than one conId left is ambiguous. Unknown or ambiguous items are left out (with their pin and
values) and named in the status line, like unknown symbols; such a file needs the connection
(`WatchlistFileHelpers.NeedsLookup`). Anything else is a plain list of symbols: one per line
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
underlying keeps streaming while a chain is shown. Every call-side cell has a faint green tint and
every put-side cell a faint red one (`CallSideBrush` / `PutSideBrush`, translucent); in-the-money
cells get a stronger tint of their side (calls below the price, puts above it —
`OptionChainHelpers.IsInTheMoney`, `CallItmBrush` / `PutItmBrush`) and the strike
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
review step. Only bid / ask cells trade with one click: they show a hand cursor and a translucent
green (buy) / red (sell) tint on hover over the side tint; last and volume cells don't react.
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

**Strike click → order ticket popup (reviewed).** The strike cell is split in two halves: the half
toward the calls opens an order ticket for that strike's call, the half toward the puts for its put.
Hovering the cell shows a small green "C" and red "P" badge at its edges, the one under the pointer
brightened, so the choice is visible before the click. The click goes through
`OrderTicketNavigator` (like Positions' "Close") and opens the Order ticket's own view in a
popup (`OrderTicketWindow`), pinned to the option, with `OptionChainHelpers.GetOrderDraft`: buy,
the chain's one-click quantity (1 when that box is invalid), the selected account, market, and the
limit price box pre-set to the option's bid / ask mid (else last; `GetMidPrice`) for switching to a
limit order. Nothing is sent from the chain: the ticket validates (`IOrderManager.ValidateOrder`),
spells the order out (contract, side, quantity, type, price, TIF, account) and sends only on "Send
order". A half is disabled while disconnected or when the strike has no option on that side.

**Columns.** The "Columns" button at the right of the status line, or a right-click on any column
header, lists the quote columns (Vol, Last, Ask, Bid) as check boxes; each hides or shows that
column on both the call and the put side at once. The strike is always shown. Each chain instance
saves its hidden columns in its placement (`WidgetPlacementEntity.OptionHiddenColumns`, null =
none; unknown values in an edited file are dropped — `OptionChainHelpers.ResolveHiddenColumns`).

A linkable view model implements `ILinkable` (`LT.App/Workspace`) by holding a `ContractLink`
(`src/Widgets/LT.Widgets.Core/Stores`): `Publish(contract)` to send, `ContractReceived` to follow; it never gets its
own publish back. The frame shows the link button for any `ILinkable`, and the workspace saves
the group in `WidgetPlacementEntity.LinkGroup`. Layouts from before link groups load pinned
charts as Not linked.

### Dragging a ticker onto a chart

A row of the Watchlist, Positions, Orders or Executions can be dragged onto any chart: the
chart the ticker is dropped on shows it, like one picked in its own ticker box (it also goes to
the recent tickers) and sends it to its link group, so every widget in the chart's group follows
it. A chart that is Not linked switches alone; widgets in other groups stay put. Remembered (stale) positions, orders and fills don't drag, like
they don't link. The press still selects the row (and so sends it to the source's group).

- A left press on a row turns into a drag once it moves 6 px (`WorkspaceHelpers.IsBeyondDragThreshold`,
  like widget headers); clicks, double-clicks, scrolling and presses on buttons inside a row
  (Close, Quick exit, Cancel) never start one.
- The option chain's strike cell drags an option: its left (C) half the call, its right (P)
  half the put of that strike and expiration. The chart then shows the option's own bars (IB
  TRADES history by conId, like any contract; an account without option data permissions gets
  IB's error in the chart). A drag cancels the half's click, so it doesn't also open the order
  ticket.
- While a ticker hovers a chart, the chart shows the `Border.dropTarget` highlight with
  "Show AAPL"; anything else dragged over it is refused.
- Plumbing in `LT.Widgets.Core`: `Helpers/TickerDragDrop` — attached `IsSource` on the list
  (its view model implements `Workspace/ITickerDragSource`, which says what contract a row
  item carries), `Contract` on a single control that carries its own contract (the strike
  halves' panels: a button's press is its own, so the row lookup can't be used, and the panel
  still takes the press while the button is disabled), `IsTarget` on the drop area (its view
  model implements `ITickerDropTarget`), and `HoverText` for the highlight to bind to. The
  payload is the row's `ContractDto` in an in-process `DataFormat` (Avalonia 12 `DataTransfer` /
  `DragDrop.DoDragDropAsync`), so it never reaches other applications.

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

**Hidden widgets hold no lines.** The workspace tells every widget view model whether it is on
screen (`IWidgetVisibility.SetShown`, in `LT.Widgets.Core/Workspace`): `WorkspaceViewModel`
works out `WidgetItem.IsShown` — a center widget only in the shown view, a side or bottom widget
only while expanded (or fullscreen), a floating widget unless its own window is minimized
(`WidgetItem.IsFloatMinimized`), and nothing of a minimized workspace window but its floating
widgets (`IsWindowMinimized`; `WorkspaceView` follows both windows' `WindowState`). A widget being
moved (drop, spawn, another window) is told once, where it lands, so a move from one shown place to
another doesn't restart its streams. A view model the workspace never tells (the order ticket in
the Close-position dialog) counts as shown.

`ConnectedViewModelBase` turns that into a second scope inside the connection session: the
**streams** scope, live while connected *and* shown. `OnStreamsStarted(token)` runs on connect
(after `OnConnected`) and when shown again; `OnStreamsStopped()` when hidden and before
`OnDisconnected`; `StreamsToken` / `IsStreaming` for streams started later (a new contract, a
chain that finished loading); `OnShownChanged()` for anything that follows visibility whether
connected or not. Market data streams belong in this scope, loads and orders in the session.
A hidden widget keeps its rows and last values, so showing it again only waits for fresh quotes.
Using it: the option chain (underlying and every row), the order ticket (its quote line) and Depth
(its depth subscription, `_isOpen && IsShown`). The watchlist already streams only rows its grid
reports on screen (none while hidden; see Watchlists). Charts and news keep streaming while
hidden: a chart's bars and a news feed would miss what arrives meanwhile.

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
| Esc (chart) | Put back an order line being dragged or waiting for its confirmation; else drop a drawing being placed or moved; else disarm the toolbox's armed tool (placing an alert, a drawing tool → the cursor); else drop the selected drawing |
| Delete / Backspace (chart, drawing mode) | Remove the selected drawing (not while it or all drawings are locked) |
| Ctrl+Z / Ctrl+Y, Ctrl+Shift+Z (chart, drawing mode) | Undo / redo the symbol's last drawing change |
| Ctrl+C / Ctrl+V / Ctrl+D (chart, drawing mode) | Copy the selected drawing / paste the copied one (any chart) / duplicate the selected one |
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
alerts, Manage indicators…, Apply template, Templates…, Show toolbox). Drawings (trend lines,
shapes, Fibonacci, measures, notes) are drawn on the price chart; in drawing mode they take the
pointer instead (see *Drawing mode*). The account performance widget's `EquityCurveChart` and
`DailyPnlBarChart` are drawn the same way (same palette, `ChartDrawing`).

**Toolbox.** A thin bar between the widget header (ticker, timeframe, period, RTH / ETH, bar
style) and the plot holds the chart's tools (`ChartViewModel.Tools`, one `ChartToolItem` per `ChartTool`;
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

**Drawing mode.** The toolbox's first button, **Draw** (highlighted while on), turns drawing mode
on for that chart (`ChartViewModel.IsDrawingMode`, saved per chart as `ChartDrawingMode`; null in
older layouts is off): a palette of tools appears down the plot's left edge
(`ChartViewModel.DrawingToolGroups`, one `ChartDrawingToolItem` each; it scrolls when the chart is
short), grouped and split by lines — cursor; trend line, ray, extended line, horizontal line,
horizontal ray, vertical line, arrow; parallel channel; rectangle, ellipse, triangle; Fibonacci
retracement and trend-based extension; price range and date range; text and brush; then the
toggles magnet and keep drawing, undo and redo, and the actions delete selected, remove all
(asked first, over the plot), lock and hide. Icons are stroked paths on the 24 × 24 grid in the
button's colour; each has a tooltip saying how to draw it.

- **Drawing.** A tool's button arms it (the button again: the cursor; arming one disarms the
  Alert tool and vice versa; disabled while the chart has no bars). Each click on the price chart
  places a point — or press, drag and release places two — with a preview following the pointer;
  one-point tools (horizontal line / ray, vertical line, text) are done on the click, three-point
  ones (channel: the trend line then a point its parallel goes through; triangle; extension: the
  move's start and end, then where the next leg starts) on the third. The brush draws while the
  button is held. A finished drawing is selected and the cursor armed again — unless **keep
  drawing** is on (a palette toggle, saved per chart as `ChartKeepDrawingTool`): then the tool
  stays armed for the next one and nothing is selected, until Esc or the cursor. A text note opens a
  box at the pointer: Enter or clicking away places it, Esc drops it; double-clicking a note with
  the cursor edits it. Esc drops the drawing being placed (the tool stays armed), the right button
  too.
- **Anchoring.** Every point is a time and a price (`ChartDrawingPointDto`), so drawings follow
  pan, zoom, resize, new bars and older pages put in front; all but the brush snap to bar centres.
  With the **magnet** on (palette toggle, saved per chart as `ChartDrawingMagnet`) a placed or
  dragged point also takes the open, high, low or close of that bar nearest the pointer
  (`ChartDrawingHelpers.GetMagnetPrice`; off the bars the pointer's price); moving a whole
  drawing keeps its shape and doesn't snap.
  A time between two bars (a gap) is interpolated, before the first or after the last bar
  extrapolated at the timeframe's bar length, so a drawing can reach into the right margin
  (`ChartDrawingHelpers.GetBarPosition` / `GetTimeAt`, `ChartViewportHelpers.GetPositionX` /
  `GetPositionAt`). They are drawn on the price chart only, under the break-even, alert and
  last-price lines; a vertical line runs through every pane and tags its time on the time axis, a
  horizontal line / ray tags its price on the price axis. Fib levels: retracement 0 at the second
  point to 1 at the first, plus 1.618; extension 0 at the third point, 1 one whole move on
  (`ChartDrawingConstants` — the defaults, each in its colour; a drawing's own levels are edited
  from the style bar). The price range labels the change and %, the date range the bars and the
  time between its points.
- **Editing.** With the cursor a click selects the drawing under it (the newest on top; anchors
  shown as circles), dragging it moves it whole (bar-snapped) and dragging an anchor moves that
  point (a stroke shows only its ends). A click elsewhere drops the selection and pans as usual.
  Delete (or the palette's) removes the selected one. **Lock** stops selecting, moving and
  deleting them one by one (Remove all still works); **hide** stops drawing them; arming a tool
  shows them again. Lock and hide are saved per chart (`ChartDrawingsLocked`,
  `ChartDrawingsHidden`).
- **Style bar.** While a drawing is selected (the cursor armed, drawings not locked or hidden all
  together) a bar floats at the bottom of the plot (`ChartViewModel.DrawingStyle.cs`): colour
  (twelve swatches or a typed `#RRGGBB` / `#AARRGGBB`; not on the price range, which is green up and
  red down), line width (1–4) and style (solid, dashed, dotted; not on a note), Fibonacci levels
  (fib drawings), lock, hide, duplicate and delete. Fills are the colour at 15 %. A new style is
  also remembered as its tool's (`IChartDrawingManager.GetDefaultStyle`, saved as `ToolStyles` in
  `chart-drawings.json`): the tool's next drawings and its preview start with it; drawings already
  there keep theirs.
- **Fibonacci levels.** The style bar's levels button opens an editor on a working copy: each
  level's value (typed as 0.65 or 65 %), shown or not, its colour (Auto: the drawing's), remove;
  add a level (kept in order), Defaults (the kind's levels back), Apply (one undo step; the editor
  stays open). Up to 24 levels, each −10 … 10 and there once.
- **Per-drawing lock and hide.** A drawing locked by itself can still be selected (its anchors
  filled) to restyle or unlock it, but not moved, reshaped, its note edited or deleted one by one.
  One hidden by itself isn't drawn or hit; right-click → *Show N hidden drawings* brings them
  back (with the global hide off). Right-clicking a drawing selects it and the menu adds copy,
  duplicate, lock / unlock, hide and delete; *Paste drawing* shows while one is copied.
- **Undo / redo.** Ctrl+Z, Ctrl+Y or Ctrl+Shift+Z (and the palette's buttons) step through the
  symbol's history: create, move, reshape, note edit, style, levels, lock / hide, delete, remove
  all and paste are each one step; undoing a delete puts the drawing back in its place in the
  drawing order. The history is per contract and shared by every chart of the symbol (an undo on
  one chart may step back an edit made on another), in memory only, at most 100 steps; a new
  change drops what was undone. Only in drawing mode.
- **Copy / paste.** Ctrl+C copies the selected drawing to the app's clipboard
  (`ChartDrawingClipboardStore`, not the system one); Ctrl+V pastes it on any chart — another
  symbol's too — 3 bars right and 16 DIPs lower (`ChartDrawingConstants.PasteOffsetBars` /
  `PasteOffsetDips`, `ChartDrawingHelpers.OffsetPoints`), selected; the next paste goes on from
  that one. Duplicate (Ctrl+D, the style bar or the menu) does the same from the selected drawing
  and leaves the clipboard alone. A copy keeps the style, note and levels, not the lock or hide.
- **Off.** With drawing mode off the palette is hidden and the drawings stay on the chart, but they
  don't take the pointer: drag pans, double-click resets, the alert tool works as without them.
- **Kept per contract.** `IChartDrawingManager` (`ChartDrawingManager`, a singleton) holds the
  drawings of every contract by ConId, saved in `chart-drawings.json`; every chart of a symbol
  shows the same ones whatever its timeframe, and a change on one reaches the others
  (`DrawingsChanged`, posted to the UI thread). Validation: `CreateChartDrawingRequest` /
  `UpdateChartDrawingRequest` / `UpdateChartDrawingPropertiesRequest` (style and level rules
  shared in `ChartDrawingRuleExtensions`), messages `ChartDrawingValidationCodes`, limits
  `ChartDrawingConstants` (500 per contract, 5,000 in all, 2,000 points per stroke, 500
  characters per note, widths 1–4, 24 fib levels in −10 … 10). `ChartDrawingKind` and
  `ChartDrawingLineStyle` are saved as numbers: append only. Not part of a chart template.
- **Older files.** A drawing's `Style`, `IsLocked`, `IsHidden` and `FibLevels` and the file's
  `ToolStyles` are missing in files written before them: such drawings read with the kind's
  built-in style (blue, solid, 1.5; fib levels 1; a note's text light — the old look), unlocked,
  shown, with the default levels (`ChartDrawingHelpers.GetBuiltInStyle` /
  `GetDefaultFibLevels`, in `ChartDrawingDtoMapper`). A drawing's levels stay null in the file
  until edited.

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

**Working orders.** The charted contract's working orders of the selected account
(`OpenOrdersStore`, one `reqAllOpenOrders` reading for every chart, every 10 s and right after
this app's orders change) are lines across the price chart: green for a buy, red for a sell, one
at the limit price and a dashed one at the stop price of STP / STP LMT / MIT / LIT orders, with a
tag at the plot's right end ("SELL 1 LMT 1.60", what is left to fill, masked in privacy mode)
and the price on the axis (`CandlestickChart.Orders.cs`, `ChartOrderHelpers.GetOrderLines`).
The limit line of **this app's own limit orders** drags up and down; the price snaps to the
contract's minimum tick (`IFootprintManager.GetTickSizeAsync`, the cached contract details) and
the tag shows old → new as it goes. Dropping it at a new price asks over the plot ("Change the
order's limit price?", side, quantity, symbol, old → new, time in force, account) and only
**Send** changes the order (`IOrderManager.ModifyOrderAsync`); Cancel or Esc puts it back, and so
does Esc while dragging. A sent change stays drawn dotted at the new price ("pending") until the
orders are read again, which then show what IB holds; IB's refusal (e.g. 110, off the tick) puts
the line back at once and shows IB's reason; no answer within 10 s says so and the next reading
decides. If the order fills, is cancelled or is changed elsewhere while the question is open,
the question is dropped. Lines that can't move — orders placed in TWS or by another client (IB
takes a change only from the client that placed the order), stop prices, non-limit orders —
are drawn but don't drag, and hovering one shows why in a tooltip
(`OrderHelpers.GetModifyBlock`). Orders off the visible price range are not drawn.

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

**Footprint bars.** The last button of a chart's header (*Candles* / *Footprint*,
`ChartViewModel.BarStyle`, `ChartBarStyle`, saved per chart as `ChartBarStyle`; null in older
layouts is candles; not part of a template) switches that chart to footprint bars
(`CandlestickChart.Footprint.cs`): for each price of a bar, the volume traded at the bid (sellers,
left) × at the ask (buyers, right), cells shaded grey by volume, diagonal imbalances — the ask
against the bid one price lower in blue, the bid against the ask one price higher in red, at
Settings → General → Charts → *Footprint imbalance* (110–1000 %, default 300) — the bar's point of
control (most volume) boxed amber, and a thin candle down the middle (white up, red down). A
volume profile of the visible footprints lies along the plot's right edge, its point of control
orange. The hovered bar's readout adds its delta and bid / ask volume. Switching opens the bars to
96 px (footprint bars zoom to 320 px, candles to 40); zooming out merges ticks into rows of 1, 2,
5, 10 … (`FootprintHelpers.GetTicksPerRow`) so rows stay 13 px for numbers (3 px for shading),
a bar shows its numbers only when every one of them fits its cells (never clipped or overlapping),
only shading below that, and plain candles under 14 px. Logic in `FootprintHelpers` (quote and
tick rules, `AddTrade`, `GetRows`, `FormatVolume`).

- **Trades.** After the bars load, `ChartFootprint` (one per load, UI thread) takes live trades
  from the current whole second on (`IFootprintManager.SubscribeTradesAsync`: tick-by-tick
  `AllLast` + `BidAsk`, two of IB's five — see
  [ibkr-tws-api.md](ibkr-tws-api.md#tick-by-tick)) and fills the past bars from the footprint
  trade cache first, then from IB pages for what the cache lacks
  (`IFootprintManager.GetTradesAsync`, IB historical ticks, 1000 a page, one at a time — see
  [ibkr-tws-api.md](ibkr-tws-api.md#historical-ticks)), each bar filled from its end towards
  its start. Pages go where the view is: the chart raises the bars it shows after each render
  (`CandlestickChart.VisibleBarsChanged` → `ChartViewModel.ShowFootprintBars`), and each visible
  bar gets one page first (newest first), then the rest of each, newest first; then the session
  back to the first bar of the latest bar's day in the display zone, in the background. Scrolling
  back to older bars pages them too. At most `FootprintConstants.MaxBackfillRequests` (1200)
  pages per load. Trades are counted into each bar's price levels as they arrive (no raw ticks
  kept in memory); what is counted is a set of stretches of time, each holding every trade in it.
  Past trades have no bid / ask, so their side goes by the tick rule, and the run at the start of
  a stretch counts half on each side until the stretch before it is in. Live trades go by the
  quote rule (the tick rule when IB gives no quotes). Row prices are the contract's minimum tick
  (`GetTickSizeAsync`, contract details cached with the trading hours); prints outside the chart
  bar's high / low (late reports IB's bars leave out) are not drawn.
- **Trade cache.** IB pages ~22 a minute, so a SPY session takes 20+ minutes to backfill; every
  trade paged or streamed is therefore kept on disk and a chart reopened (or the app restarted)
  shows those bars at once, asking IB only for the gaps. `IFootprintTapeRepository`
  (`FootprintTapeRepository`, singleton) keeps raw trades — time (whole seconds), price, size and
  the quote rule's side for live trades that had one — not bars, so every timeframe, RTH and ETH
  share it: `<app-data>/footprint-trades/<conId>/<yyyy-MM-dd>.lttape`, one file per UTC day.
  A file is a header and appended frames (`FootprintTapeHelpers`): each frame is one stretch of
  time with every trade in it, varint-packed (~3.4 bytes a trade),
  with its length and an FNV-1a checksum, so a stretch counts as recorded only together with its
  trades — a torn or corrupt frame (a crash mid-write) is dropped with everything after it and cut
  off before the next append. What each file covers is read once per run; appends skip what is
  already there (two charts of one symbol, a page reaching over recorded trades). Pages are
  recorded by `FootprintManager.GetTradesAsync` as they arrive, from their oldest trade's second
  to their end, but at most up to `TapeHistoricalSettle` (30 s) before now (IB's history may lack
  the last moment's trades); IB is asked outside regular hours too, so the cache holds every
  trade. Live trades are recorded by `SubscribeTradesAsync` in batches of
  `TapeLiveFlushInterval` (2 s), from the second after the first trade (that second may have
  begun before the stream) to the last trade's second when the stream ends; trades counted by
  the tick rule are stored without a side and told again on load, so cached and paged trades get
  the same sides. On load, `ChartFootprint.GetNextRecordedDay` names the UTC days the pages would
  go to, in the same order (visible bars first, newest first, then the session);
  `ChartViewModel` reads each (`IFootprintManager.GetRecordedTradesAsync`, off the UI thread) and
  counts it `TapeTradesPerSlice` (100,000) trades at a time, the chart drawing in between, before
  any page of that day. `GetRecordedTradesAsync` prunes first: days older than
  `TapeRetentionDays` (30), then the oldest days past `TapeMaxBytes` (2 GB). Not part of
  Settings' cache (Clear cache and its size limit don't touch it). Measured on SPY, 2026-10-29:
  311,000 real trades (11:25–16:00 New York) took 1.05 MB, read in 13 ms and folded into
  5-minute bars in 32 ms; a full day of 1.1 M trades (tiled from those) is 3.5 MB, read in
  ~50 ms and folded in ~100 ms in slices of at most 10 ms. Reopening the chart showed the 54
  bars complete before complete at 0.5 s, with no IB request for them.
- **Loading.** A bar shows the trades it has as soon as its first page is in (the forming bar
  its live trades at once), with a dotted line over it until all are in (`Partial`; the readout
  says from when). Before its first page it is a thin candle with three dots above; past the
  backfill (the cap, the start of IB's data, a failed page) or once the live stream failed
  (limit reached, refusal: "not live" in the status line and the message log) a candle with a
  small ring above. The status line says "footprint loading… 3/14 bars on screen complete",
  then "footprint from 14:35, loading…" while the session fills, "footprint" once it is
  complete or "footprint from 14:35" when it stopped short. IB answers ~22 pages a minute after
  its first ~10, so a heavy symbol fills slowly: SPY's last bar of the day alone is ~34 pages.
  Daily and weekly charts and forex (no trades) stay candles and say why.
- Not built yet: a session (instead of visible-range) profile, cell text sizes / colours as
  settings, stacked imbalances.

## Styling

- Colours/brushes as resources in `App.axaml`; positive/negative P/L via the `positive` /
  `negative` classes, never hard-coded in a view.
- Widgets sit in a `Border.panel` with a `TextBlock.panelTitle` header.
- A number box in a header or status row uses `NumericUpDown.compact` (24px, centred text, slim
  `CompactButtonSpinner` with flat chevrons). Fluent sizes its spinner buttons inside the
  template, so shrinking `MinHeight`/`Padding` on the control alone only squashes the text.
