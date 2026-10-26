using System.Collections.ObjectModel;
using LT.App.Shell;
using LT.App.Stores;
using LT.App.Widgets.Account;
using LT.App.Widgets.AccountPerformance;
using LT.App.Widgets.Chart;
using LT.App.Widgets.DayTradePerformance;
using LT.App.Widgets.Executions;
using LT.App.Widgets.Orders;
using LT.App.Widgets.Positions;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Settings;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;
using LT.Contracts.Interfaces.IManagers;

namespace LT.App.Workspace;

/// <summary>
/// Every workspace of the app: the main window's and one per extra workspace window
/// (menu → New window). Each has its own docks, center views and floating widgets, with
/// instances of their own of any widget.
/// Link groups and the other stores stay app-wide. Singleton (Program.cs); the main window opens a
/// window for every entry of <see cref="Windows"/>.
/// </summary>
public class WorkspaceWindows : IWorkspaceHost
{
	private readonly WidgetViewModelFactory _widgetViewModelFactory;
	private readonly AppearanceStore _appearance;

	public WorkspaceWindows(
		WidgetViewModelFactory widgetViewModelFactory,
		ISettingsManager settingsManager,
		AppearanceStore appearance
	)
	{
		_widgetViewModelFactory = widgetViewModelFactory;
		_appearance = appearance;
		var layout = settingsManager.GetLayout();
		WorkspaceWindowHelpers.Normalize(layout);
		Main = Create(layout);
		foreach (var window in layout.Windows)
			Windows.Add(new WorkspaceWindowViewModel(this, window, Create(window)));
	}

	/// <summary>
	/// The main window's workspace. Replaced when the main window is closed on its own while extra
	/// windows are open (<see cref="ReplaceMain"/>).
	/// </summary>
	public WorkspaceViewModel Main { get; private set; }

	/// <summary>Raised when <see cref="Main"/> is replaced.</summary>
	public event Action? MainChanged;

	/// <summary>The extra windows, in the order they were opened.</summary>
	public ObservableCollection<WorkspaceWindowViewModel> Windows { get; } = [];

	public IEnumerable<WorkspaceViewModel> Workspaces =>
		Windows.Select(x => x.Workspace).Prepend(Main);

	/// <summary>
	/// Set while the app closes: extra windows closing then are kept for the next start instead of
	/// being discarded.
	/// </summary>
	public bool IsExiting { get; set; }

	/// <summary>Menu → New window (Ctrl+Shift+N): an empty workspace in a window of its own.</summary>
	public WorkspaceWindowViewModel Open()
	{
		var layout = WorkspaceWindowHelpers.CreateEmpty(Windows.Select(x => x.Name));
		var window = new WorkspaceWindowViewModel(this, layout, Create(layout));
		Windows.Add(window);
		return window;
	}

	/// <summary>The user closed the window: it is forgotten and its widgets are disposed.</summary>
	public void Discard(WorkspaceWindowViewModel window)
	{
		if (Windows.Remove(window))
			window.Workspace.DisposeWidgets();
	}

	/// <summary>
	/// The main window's ✕ → Close this window, with extra windows open: the main window can't go
	/// (it holds the toolbar, status bar and connection, and the app exits with it), so the first
	/// extra window's workspace moves into it and that window is forgotten; the main workspace is
	/// discarded with its widgets, like a closed extra window. Returns the window taken over (its
	/// bounds are where the main window goes), null when there is none.
	/// </summary>
	public WorkspaceWindowViewModel? ReplaceMain()
	{
		if (Windows.Count == 0)
			return null;
		var next = Windows[0];
		var old = Main;
		Windows.RemoveAt(0);
		Main = next.Workspace;
		MainChanged?.Invoke();
		old.DisposeWidgets();
		return next;
	}

	public IReadOnlyList<ViewMoveTarget> GetMoveTargets(WorkspaceViewModel from) =>
		Windows
			.Select(x => new ViewMoveTarget(x.Name, x.Workspace))
			.Prepend(new ViewMoveTarget(WorkspaceConstants.MainWindowName, Main))
			.Where(x => x.Workspace != from)
			.ToList();

	public void MoveView(CenterViewItem view, WorkspaceViewModel to, int? slot = null)
	{
		var from = view.Workspace;
		if (from == to)
		{
			if (slot is { } s)
				from.MoveView(view, s);
			return;
		}
		if (!Workspaces.Contains(to) || from.ReleaseView(view) is not { } transfer)
			return;
		to.AdoptView(transfer, slot);
		CloseIfEmpty(from);
	}

	public void DetachView(CenterViewItem view, double? x = null, double? y = null)
	{
		var from = view.Workspace;
		if (!from.Views.Contains(view))
			return;
		var layout = WorkspaceWindowHelpers.CreateEmpty(Windows.Select(w => w.Name));
		layout.X = x;
		layout.Y = y;
		var window = new WorkspaceWindowViewModel(this, layout, Create(layout));
		if (from.ReleaseView(view) is not { } transfer)
			return;
		// The new workspace starts with an empty view: the detached one takes its place
		var placeholder = window.Workspace.Views[0];
		window.Workspace.AdoptView(transfer);
		window.Workspace.CloseView(placeholder);
		Windows.Add(window);
		CloseIfEmpty(from);
	}

	/// <summary>
	/// An extra window whose last view was moved out and that shows nothing else is closed (the
	/// main window stays).
	/// </summary>
	private void CloseIfEmpty(WorkspaceViewModel workspace)
	{
		if (
			workspace.IsEmpty && Windows.FirstOrDefault(x => x.Workspace == workspace) is { } window
		)
			Discard(window);
	}

	public void FillLayout(LayoutDto layout) =>
		layout.Windows = Windows.Select(x => x.GetLayout()).ToList();

	/// <summary>F5: reloads every open widget in every window.</summary>
	public void RefreshAll()
	{
		foreach (
			var content in Workspaces
				.SelectMany(x => x.Widgets)
				.Where(x => x.IsOpen)
				.Select(x => x.Content)
		)
		{
			switch (content)
			{
				case ChartViewModel chart:
					chart.Reload();
					break;
				case AccountViewModel account:
					account.Refresh();
					break;
				case AccountPerformanceViewModel performance:
					performance.Refresh();
					break;
				case PositionsViewModel positions:
					positions.Refresh();
					break;
				case OrdersViewModel orders:
					orders.Refresh();
					break;
				case ExecutionsViewModel executions:
					executions.Refresh();
					break;
				case DayTradePerformanceViewModel performance:
					_ = performance.RefreshAsync();
					break;
			}
		}
	}

	private WorkspaceViewModel Create(WorkspaceLayoutDto layout) =>
		new(_widgetViewModelFactory.Create, layout, _appearance, this);
}
