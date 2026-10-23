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
- **Shell** (`Shell/MainWindow`) = menu, connection bar, the current page, status bar.

ViewModels are resolved from DI. They have no interfaces, so the LSCore convention scan skips them —
register each one as transient in `Program.cs` (`builder.Services.AddTransient<FooViewModel>()`).
Small, data-only VMs (`PlaceholderViewModel`) are constructed by their parent instead.

## Threading — the one rule that matters

IB callbacks and `IAsyncEnumerable` continuations run **off the UI thread**. Never touch an
observable property or collection bound to the UI from there:

```csharp
connectionManager.StatusChanged += status => Dispatcher.UIThread.Post(() => Apply(status));
```

For high-frequency streams (ticks) don't post per tick — coalesce into the latest value and flush
on a `DispatcherTimer` (e.g. every 100–250 ms).

## Lifetime of subscriptions

A widget that subscribes to a stream holds a `CancellationTokenSource`, cancels it when the
connection drops or the widget is closed. Cancelling ends the `await foreach`, which cancels the IB
subscription and frees the market-data line.

## Styling

- Colours/brushes as resources in `App.axaml`; positive/negative P/L via the `positive` /
  `negative` classes, never hard-coded in a view.
- Widgets sit in a `Border.panel` with a `TextBlock.panelTitle` header.
