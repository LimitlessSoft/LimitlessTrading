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

## Settings window

`Widgets/Settings`: a searchable tree of pages on the left, the selected page on the right. A page
(`SettingsPageViewModel`) has tabs (`SettingsTabViewModel`; the tab strip shows only with more than
one); a group (no tabs) holds child pages and opens its first one. Each setting is a
`SettingItemViewModel` (`BoolSettingViewModel`, `ChoiceSettingViewModel`,
`NumberSettingViewModel`, one DataTemplate each) with a title, help text and search keywords.
Search (`SettingsSearchHelpers.Matches`: every word, any order) keeps the pages, tabs and settings
that match; a page, group or tab whose own title matches shows all of its settings. Changes apply
on Save. Adding a setting: a field in `SettingsViewModel`, put it on a tab, read and save it there.

## Workspace (`LT.App/Workspace`)

Every widget is **independently spawnable** and lives in one of four wrappers:

| Wrapper | Behaviour |
|---|---|
| **Center** | A split layout: widgets side by side and/or stacked, nested to any depth, with a splitter between every pair. |
| **Left / right side** | Each widget docked there gets an icon on that edge's strip. One is expanded at a time; expanding one (its icon, Ctrl+1…9, the menu) makes it current and gives it keyboard focus; clicking its icon again collapses the side and the center grows. Each widget keeps its own panel width (`WidgetItem.SideWidth`, saved in the layout); one without a width of its own starts at the side's last width. |
| **Bottom** | Like a side, along the bottom of the center column (the side panels keep their full height): a horizontal icon strip, shown only while a widget is docked there, and above it one expanded widget at a time. Each widget keeps its own panel height (`WidgetItem.BottomHeight`); a collapsed bottom leaves no gap. |
| **Floating** | The widget gets its own window (owned by the main window). Closing that window closes the widget. |

- Spawn or reveal a widget from menu → Widgets. Ctrl+1 … Ctrl+9 do the same, but collapse a side
  widget that is already expanded.
- **Charts can be open several times** (menu → Widgets → New chart, Ctrl+Shift+7). Each chart has
  its own timeframe, link group and contract, all saved per chart. The chart header's controls
  (RTH, timeframe, Reload) are hidden until they are redesigned; F5 reloads.
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
- A widget opens where it last was; a new chart opens where the last chart was. On first run:
  Watchlist, Positions, Orders and Executions on the left; Account and Messages on the right;
  Chart in the center; Option chain, Order ticket and News closed (`WorkspaceHelpers.GetDefaultPlacements`).
- **Order ticket** (`LT.Widgets.OrderTicket`, right side): takes its contract from the link group.
  Account, side, market / limit, quantity or amount in $ (market only), limit price, time in force
  (crypto: GTC / IOC). "Review order" validates (`IOrderManager.ValidateOrder`) and spells the
  order out with the endpoint; any edit drops the review; only "Send order" sends. Orders sent
  this session list their status below, with Cancel while working; everything is also logged
  to Messages.
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
- The frame (and the widget view inside it) is created once per instance and **moved** between
  hosts, so state and subscriptions survive docking, floating and collapsing. Widget view models
  are created on first open by `WidgetViewModelFactory`.
- `WorkspaceViewModel` owns placement: the center split tree (`CenterRoot`, a `CenterNodeDto`
  edited by `CenterLayoutHelpers`), the `Left` / `Right` / `Floating` collections and the
  expanded widget per side. `WorkspaceView` only mirrors it: the center is rebuilt as nested
  grids whenever the tree changes, splitter sizes are written back into the tree after a drag.
- Saved in `LayoutEntity`: `Widgets` (id, kind, dock, floating bounds, chart settings of each open
  instance), `Center` (the split tree with sizes), the expanded widget per side and side widths.

**Adding a widget** (in its own project, like `LT.Widgets.Watchlist`): a `WorkspaceTool` value
(at the end); a `src/Widgets/LT.Widgets.<Widget>` project referencing `LT.Widgets.Core`, added to the
solution's `Widgets` folder and referenced by `LT.App`; an `IWidgetDefinition` there (title, icon, focus key, view model type,
view and optional header title) listed in `WidgetCatalog.Definitions` — that also registers its
view model in DI; a default dock in `WorkspaceHelpers.GetDefaultDock`, and a menu entry in
`MainWindow.axaml`. Per-instance settings: implement `IWidgetState` on the view model.

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
(`WatchlistHelpers.EnsureWatchlists`).

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

Watchlist, Positions, Chart, Option chain and News can be linked: the chain icon left of the frame's
close button opens a menu of ten color groups (Red, Green, Blue, Yellow, Orange, Purple, Cyan,
Pink, Lime, White) or Not linked, and is tinted
with the current one. Widgets in the same group show the same contract:

- Clicking a watchlist or positions row, or picking a chart's ticker, sends that contract to the
  group; charts and option chains in the group switch to it.
- Joining a group shows the group's contract; joining an empty group gives it the widget's own.
- New widgets start in Red, so out of the box they all follow each other. "Not linked" keeps a
  widget on its own contract.

The option chain has no ticker input of its own: its underlying comes from its group, and the
frame header shows that ticker (`WidgetViewFactory.CreateTitle` binds `OptionChainViewModel.Title`).
It lists one tab per expiration (`IOptionChainManager.GetExpirationsAsync`, from
`reqSecDefOptParams`); the selected tab loads that day's strikes around the money.

A linkable view model implements `ILinkable` (`LT.App/Workspace`) by holding a `ContractLink`
(`src/Widgets/LT.Widgets.Core/Stores`): `Publish(contract)` to send, `ContractReceived` to follow; it never gets its
own publish back. The frame shows the link button for any `ILinkable`, and the workspace saves
the group in `WidgetPlacementEntity.LinkGroup`. Layouts from before link groups load pinned
charts as Not linked.

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
| F5 | Refresh chart, account, positions, orders, executions |
| Ctrl+, | Settings |
| Ctrl+1 … Ctrl+9 | Show Watchlist, Positions, Orders, Executions, Account, Messages, Chart, Option chain, Order ticket (collapses an expanded side widget) |
| Ctrl+Alt+W / P / O / E / A / M / C / B / N | Show and focus Watchlist, Positions, Orders, Executions, Account, Messages, Option chain, Order ticket, News (single-instance widgets; works from floating windows too) |
| Esc (side or bottom widget) | Back to the last focused center widget; an unpinned widget collapses |
| Ctrl+Shift+7 | New chart |
| Ctrl+Shift+F | Fullscreen the current widget (the one last clicked or typed in, framed in light blue), or restore it |

Ctrl+Shift+*key* combos act on a widget.

The window handles them in a tunnelling `KeyDown` handler (`MainWindow.axaml.cs`), so they work
while a text box has focus. On macOS, Cmd works in place of Ctrl.

## Charts

`Widgets/Chart/CandlestickChart` is a custom `Control` drawn with `DrawingContext` (no chart
library): candles, volume, price grid, last-price line, crosshair + OHLCV readout. Wheel zooms,
drag pans, double-click resets.

## Styling

- Colours/brushes as resources in `App.axaml`; positive/negative P/L via the `positive` /
  `negative` classes, never hard-coded in a view.
- Widgets sit in a `Border.panel` with a `TextBlock.panelTitle` header.
