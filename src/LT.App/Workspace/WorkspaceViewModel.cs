using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LT.App.Stores;
using LT.App.ViewModels;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Settings;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;

namespace LT.App.Workspace;

/// <summary>
/// Where every widget instance lives: in the split layout of one of the center's views (tabs), on a
/// collapsible strip (left, right or bottom), or in its own floating window. Widgets are spawned,
/// moved and closed independently; charts can be spawned several times. The main window and every
/// extra workspace window have one each; a single-instance widget exists in only one of them.
/// </summary>
public partial class WorkspaceViewModel : ViewModelBase
{
	private readonly Func<WorkspaceTool, object> _createContent;

	// Takes a single-instance widget out of whichever other workspace holds it (null: none does)
	private readonly Func<WorkspaceTool, WidgetItem?>? _claim;

	// The other windows, where a view can move to (null: none, e.g. offline views)
	private readonly IWorkspaceHost? _host;

	// Last dock per widget kind, for spawning new instances where the previous one was
	private readonly Dictionary<WorkspaceTool, WidgetDock> _lastDock = [];

	// Center widget last clicked or typed in: where Esc in a side or bottom widget returns focus
	private string? _lastCenterId;

	public WorkspaceViewModel(
		Func<WorkspaceTool, object> createContent,
		WorkspaceLayoutDto layout,
		AppearanceStore? appearance = null,
		Func<WorkspaceTool, WidgetItem?>? claim = null,
		IWorkspaceHost? host = null
	)
	{
		_createContent = createContent;
		_claim = claim;
		_host = host;
		Appearance = appearance;
		Bottom.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasBottomWidgets));
		LeftWidth = layout.LeftWidth;
		RightWidth = layout.RightWidth;
		BottomHeight = layout.BottomHeight;

		var centerIds = new List<string>();
		foreach (var placement in WorkspaceHelpers.Normalize(layout.Widgets))
		{
			var widget = CreateItem(placement.Id, placement.Tool);
			LoadPlacement(widget, placement);
			if (placement.Dock == WidgetDock.Center)
			{
				SetDock(widget, WidgetDock.Center);
				centerIds.Add(widget.Id);
			}
			else
				Place(widget, placement.Dock);
		}
		var views = CenterViewHelpers.Normalize(
			layout.Views,
			layout.Center,
			layout.SelectedViewId,
			centerIds
		);
		foreach (var view in views)
			Views.Add(new CenterViewItem(this, view.Id, view.Name) { Root = view.Root });
		var selectedId = CenterViewHelpers.GetSelectedId(views, layout.SelectedViewId);
		SelectedView = Views.First(x => x.Id == selectedId);
		Views.CollectionChanged += (_, _) => UpdateViewFlags();
		UpdateViewFlags();

		LeftActive = Left.FirstOrDefault(x => x.Id == layout.LeftWidgetId);
		RightActive = Right.FirstOrDefault(x => x.Id == layout.RightWidgetId);
		BottomActive = Bottom.FirstOrDefault(x => x.Id == layout.BottomWidgetId);
	}

	/// <summary>
	/// Cards or flush look. Null (offline views, no store) is cards: without the store nothing sets
	/// the window's flush class, so the geometry stays matched to the frames.
	/// </summary>
	public AppearanceStore? Appearance { get; }

	/// <summary>Every instance that exists: open ones and closed single-instance widgets.</summary>
	public ObservableCollection<WidgetItem> Widgets { get; } = [];

	public ObservableCollection<WidgetItem> Left { get; } = [];
	public ObservableCollection<WidgetItem> Right { get; } = [];
	public ObservableCollection<WidgetItem> Bottom { get; } = [];
	public ObservableCollection<WidgetItem> Floating { get; } = [];

	/// <summary>The center's tabs, each with its own split tree; never empty.</summary>
	public ObservableCollection<CenterViewItem> Views { get; } = [];

	/// <summary>The view shown in the center.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(HasCenterWidgets))]
	public partial CenterViewItem SelectedView { get; set; }

	/// <summary>Split tree of the shown view; null when it is empty. See <see cref="CenterChanged"/>.</summary>
	public CenterNodeDto? CenterRoot => SelectedView.Root;

	/// <summary>Raised whenever a view's tree changes shape.</summary>
	public event Action? CenterChanged;

	public bool HasCenterWidgets => CenterRoot != null;

	partial void OnSelectedViewChanged(CenterViewItem oldValue, CenterViewItem newValue)
	{
		// Null only on the first assignment, in the constructor
		oldValue?.IsSelected = false;
		newValue.IsSelected = true;
		// The widget framed as current stays only if it is still on screen
		if (CurrentWidget is { IsCenter: true } current && !IsInView(newValue, current.Id))
			CurrentWidget = null;
	}

	public void SelectView(CenterViewItem view)
	{
		if (Views.Contains(view))
			SelectedView = view;
	}

	/// <summary>"+": a new empty view after the others, shown at once.</summary>
	[RelayCommand]
	public void AddView()
	{
		var view = new CenterViewItem(
			this,
			CenterViewHelpers.NewId(),
			CenterViewHelpers.GetNewName(Views.Select(x => x.Name))
		);
		Views.Add(view);
		SelectedView = view;
	}

	/// <summary>
	/// Right-click → Duplicate: a copy of the view right after it, shown at once. Its split tree is
	/// the same, filled with new instances of the widgets carrying their settings (symbol, link
	/// group, …); single-instance widgets can't be in two places, so their slots are left out.
	/// </summary>
	public void DuplicateView(CenterViewItem source)
	{
		var index = Views.IndexOf(source);
		if (index < 0)
			return;
		var widgetIds = new Dictionary<string, string>();
		foreach (var id in CenterLayoutHelpers.GetWidgetIds(source.Root).ToList())
		{
			if (
				Find(id) is not { } widget
				|| !WorkspaceHelpers.AllowsMultipleInstances(widget.Tool)
			)
				continue;
			var copy = CreateItem(NewId(widget.Tool), widget.Tool);
			LoadPlacement(copy, SavePlacement(widget));
			SetDock(copy, WidgetDock.Center);
			widgetIds[id] = copy.Id;
		}
		var duplicate = CenterViewHelpers.Duplicate(
			source.Name,
			source.Root,
			widgetIds,
			Views.Select(x => x.Name)
		);
		var view = new CenterViewItem(this, duplicate.Id, duplicate.Name) { Root = duplicate.Root };
		Views.Insert(index + 1, view);
		SelectedView = view;
	}

	/// <summary>Closes the view and, like ✕ on each of them, the widgets in it. The last view stays.</summary>
	public void CloseView(CenterViewItem view)
	{
		var index = Views.IndexOf(view);
		if (index < 0 || Views.Count <= 1)
			return;
		if (SelectedView == view)
		{
			var remaining = Views.Where(x => x != view).ToList();
			SelectedView = remaining[CenterViewHelpers.GetIndexAfterClose(index, remaining.Count)];
		}
		foreach (var id in CenterLayoutHelpers.GetWidgetIds(view.Root).ToList())
			if (Find(id) is { } widget)
				Close(widget);
		Views.Remove(view);
		RaiseCenterChanged();
	}

	/// <summary>
	/// A tab dragged along the strip: the view goes to gap <paramref name="slot"/> (0 = before the
	/// first tab, <c>Views.Count</c> = after the last). Its widgets and tree stay as they are.
	/// </summary>
	public void MoveView(CenterViewItem view, int slot)
	{
		var from = Views.IndexOf(view);
		if (from < 0)
			return;
		var to = CenterViewHelpers.GetMoveIndex(from, slot, Views.Count);
		if (to != from)
			Views.Move(from, to);
	}

	/// <summary>Right-click → Move left / Move right: one tab over (<paramref name="step"/> −1 or 1).</summary>
	public void MoveViewBy(CenterViewItem view, int step)
	{
		var from = Views.IndexOf(view);
		var to = from + step;
		if (from < 0 || to < 0 || to >= Views.Count || to == from)
			return;
		Views.Move(from, to);
	}

	/// <summary>Views can move to other windows (the app's windows are known).</summary>
	public bool CanMoveViewsOut => _host != null;

	/// <summary>The windows a view of this workspace can move to (right-click → Move to window).</summary>
	public IReadOnlyList<ViewMoveTarget> GetMoveTargets() => _host?.GetMoveTargets(this) ?? [];

	/// <summary>Moves the view, with its widgets, to the window of <paramref name="to"/>.</summary>
	public void MoveViewTo(CenterViewItem view, WorkspaceViewModel to, int? slot = null)
	{
		if (to == this)
		{
			if (slot is { } s)
				MoveView(view, s);
			return;
		}
		_host?.MoveView(view, to, slot);
	}

	/// <summary>Right-click → Detach, or a tab dropped outside every window: a new window with only this view.</summary>
	public void DetachView(CenterViewItem view, double? x = null, double? y = null) =>
		_host?.DetachView(view, x, y);

	/// <summary>
	/// Takes the view out of this workspace, with the center widgets in it (view models and
	/// settings go along), for another workspace to adopt (<see cref="AdoptView"/>). The last view
	/// leaves an empty one in its place, so the workspace always has a view. Null when the view
	/// isn't here.
	/// </summary>
	public CenterViewTransfer? ReleaseView(CenterViewItem view)
	{
		var index = Views.IndexOf(view);
		if (index < 0)
			return null;
		var widgets = CenterLayoutHelpers
			.GetWidgetIds(view.Root)
			.Select(Find)
			.OfType<WidgetItem>()
			.ToList();
		foreach (var widget in widgets)
		{
			if (FullscreenWidget == widget)
				FullscreenWidget = null;
			if (CurrentWidget == widget)
				CurrentWidget = null;
			if (_lastCenterId == widget.Id)
				_lastCenterId = null;
			Widgets.Remove(widget);
			widget.Workspace = null;
		}
		if (Views.Count == 1)
			Views.Add(
				new CenterViewItem(
					this,
					CenterViewHelpers.NewId(),
					WorkspaceConstants.DefaultViewName
				)
			);
		if (SelectedView == view)
		{
			var remaining = Views.Where(x => x != view).ToList();
			SelectedView = remaining[CenterViewHelpers.GetIndexAfterClose(index, remaining.Count)];
		}
		Views.Remove(view);
		RaiseCenterChanged();
		return new CenterViewTransfer(view.Id, view.Name, view.Root, widgets);
	}

	/// <summary>
	/// Takes in a view released by another workspace, at tab gap <paramref name="slot"/> (null:
	/// after the others), and shows it. Its widgets keep their view models and settings; one
	/// whose id is already taken here gets a new one.
	/// </summary>
	public CenterViewItem AdoptView(CenterViewTransfer transfer, int? slot = null)
	{
		var ids = CenterViewHelpers.GetTransferIds(
			transfer.Widgets.Select(x => x.Id),
			Widgets.Select(x => x.Id)
		);
		foreach (var widget in transfer.Widgets)
		{
			widget.Id = ids[widget.Id];
			widget.Workspace = this;
			Widgets.Add(widget);
		}
		var view = new CenterViewItem(
			this,
			Views.Any(x => x.Id == transfer.Id) ? CenterViewHelpers.NewId() : transfer.Id,
			transfer.Name
		)
		{
			Root = CenterLayoutHelpers.Copy(transfer.Root, ids),
		};
		Views.Insert(slot is { } s ? Math.Clamp(s, 0, Views.Count) : Views.Count, view);
		SelectedView = view;
		RaiseCenterChanged();
		return view;
	}

	/// <summary>
	/// Nothing left to show: no open widget anywhere and a single empty view. An extra window
	/// left like this by moving its last view out is closed.
	/// </summary>
	public bool IsEmpty => Views.Count == 1 && Views[0].Root is null && !Widgets.Any(x => x.IsOpen);

	/// <summary>Ctrl+Tab / Ctrl+Shift+Tab: the next or previous view, wrapping around.</summary>
	public void CycleView(int step)
	{
		// The fullscreen widget covers the tabs: switching underneath it would go unseen
		if (Views.Count < 2 || FullscreenWidget != null)
			return;
		SelectedView = Views[
			CenterViewHelpers.Cycle(Views.IndexOf(SelectedView), Views.Count, step)
		];
	}

	public void BeginRenameView(CenterViewItem view)
	{
		foreach (var other in Views)
			other.IsRenaming = false;
		view.RenameText = view.Name;
		view.IsRenaming = true;
	}

	/// <summary>Enter or clicking away: a blank name keeps the old one.</summary>
	public void CommitRenameView(CenterViewItem view)
	{
		if (!view.IsRenaming)
			return;
		view.IsRenaming = false;
		if (CenterViewHelpers.CleanName(view.RenameText) is { } name)
			view.Name = name;
	}

	public void CancelRenameView(CenterViewItem view) => view.IsRenaming = false;

	/// <summary>The view whose tree holds the widget; null when it isn't in the center.</summary>
	public CenterViewItem? FindView(string widgetId) =>
		Views.FirstOrDefault(x => IsInView(x, widgetId));

	/// <summary>Expanded widget of each side, null when that side panel is collapsed.</summary>
	[ObservableProperty]
	public partial WidgetItem? LeftActive { get; set; }

	[ObservableProperty]
	public partial WidgetItem? RightActive { get; set; }

	[ObservableProperty]
	public partial WidgetItem? BottomActive { get; set; }

	/// <summary>The bottom strip shows only while a widget is docked there.</summary>
	public bool HasBottomWidgets => Bottom.Count > 0;

	/// <summary>Last width of each side panel: the start width of widgets without their own.</summary>
	public double LeftWidth { get; set; }
	public double RightWidth { get; set; }

	/// <summary>Last height of the bottom panel: the start height of widgets without their own.</summary>
	public double BottomHeight { get; set; }

	partial void OnLeftActiveChanged(WidgetItem? oldValue, WidgetItem? newValue) =>
		Activate(oldValue, newValue);

	partial void OnRightActiveChanged(WidgetItem? oldValue, WidgetItem? newValue) =>
		Activate(oldValue, newValue);

	partial void OnBottomActiveChanged(WidgetItem? oldValue, WidgetItem? newValue) =>
		Activate(oldValue, newValue);

	public WidgetItem? Find(string id) => Widgets.FirstOrDefault(x => x.Id == id);

	/// <summary>The widget shown over the whole window (Ctrl+Shift+F); at most one.</summary>
	[ObservableProperty]
	public partial WidgetItem? FullscreenWidget { get; set; }

	/// <summary>The widget last clicked or typed in: what Ctrl+Shift+F acts on, framed in blue.</summary>
	[ObservableProperty]
	public partial WidgetItem? CurrentWidget { get; set; }

	partial void OnCurrentWidgetChanged(WidgetItem? oldValue, WidgetItem? newValue)
	{
		oldValue?.IsCurrent = false;
		newValue?.IsCurrent = true;
		if (newValue is { IsCenter: true })
			_lastCenterId = newValue.Id;
	}

	/// <summary>
	/// Esc in an expanded side or bottom widget: an unpinned one collapses, and the center widget
	/// focused last (else the first one) becomes current and gets keyboard focus. False when there
	/// was nothing to do (a pinned widget and an empty center).
	/// </summary>
	public bool LeaveDocked(WidgetItem widget)
	{
		if (widget is not { IsSide: true, IsActive: true, IsFullscreen: false })
			return false;
		var collapsed = !widget.IsPinned;
		if (collapsed)
			Collapse(widget);
		var targetId = WorkspaceHelpers.GetFocusReturnId(
			_lastCenterId,
			CenterLayoutHelpers.GetWidgetIds(CenterRoot)
		);
		if (targetId is null || Find(targetId) is not { } target)
			return collapsed;
		CurrentWidget = target;
		target.RequestFocus();
		return true;
	}

	/// <summary>
	/// Fullscreen on/off. Without a widget (the shortcut) it restores the fullscreen one, or makes
	/// the current one fullscreen.
	/// </summary>
	public void ToggleFullscreen(WidgetItem? widget = null)
	{
		widget ??= FullscreenWidget ?? CurrentWidget;
		if (widget is not { IsOpen: true })
			return;
		FullscreenWidget = FullscreenWidget == widget ? null : widget;
	}

	partial void OnFullscreenWidgetChanged(WidgetItem? oldValue, WidgetItem? newValue)
	{
		oldValue?.IsFullscreen = false;
		newValue?.IsFullscreen = true;
	}

	/// <summary>Brings the widget forward: an open instance, else reopens or spawns it.</summary>
	/// <summary>
	/// Ctrl+Alt+key: shows the widget (opening, expanding or raising it), makes it the current one
	/// and moves keyboard focus into it.
	/// </summary>
	public void Focus(WorkspaceTool tool)
	{
		Show(tool);
		if (Widgets.FirstOrDefault(x => x.Tool == tool && x.IsOpen) is not { } widget)
			return;
		CurrentWidget = widget;
		widget.RequestFocus();
	}

	public void Show(WorkspaceTool tool)
	{
		var widget =
			Widgets.FirstOrDefault(x => x.Tool == tool && x.IsOpen)
			?? Widgets.FirstOrDefault(x => x.Tool == tool);
		// A multi-instance widget (chart) only in another view: open a new one in this view
		// instead of switching away
		if (WorkspaceHelpers.AllowsMultipleInstances(tool))
			widget = Widgets.FirstOrDefault(x =>
				x.Tool == tool
				&& x.IsOpen
				&& (x.Dock != WidgetDock.Center || SelectedView is { } view && IsInView(view, x.Id))
			);
		// A single-instance widget living in another window moves here
		else
			widget ??= Claim(tool);
		if (widget is null)
		{
			Spawn(tool);
			return;
		}
		if (widget.Dock is null)
			Place(widget, widget.LastDock ?? GetSpawnDock(tool));
		Reveal(widget);
	}

	/// <summary>Opens another instance (charts); single-instance widgets are just shown.</summary>
	public void Spawn(WorkspaceTool tool)
	{
		if (!WorkspaceHelpers.AllowsMultipleInstances(tool) && Widgets.Any(x => x.Tool == tool))
		{
			Show(tool);
			return;
		}
		if (!WorkspaceHelpers.AllowsMultipleInstances(tool) && Claim(tool) != null)
		{
			Show(tool);
			return;
		}
		var widget = CreateItem(NewId(tool), tool);
		Place(widget, GetSpawnDock(tool));
		Reveal(widget);
	}

	/// <summary>
	/// Takes the instance of a single-instance widget out of this workspace (open or closed) so
	/// another window can adopt it; its view model and settings go with it. Null when this
	/// workspace doesn't hold it.
	/// </summary>
	public WidgetItem? Release(WorkspaceTool tool)
	{
		if (
			WorkspaceHelpers.AllowsMultipleInstances(tool)
			|| Widgets.FirstOrDefault(x => x.Tool == tool) is not { } widget
		)
			return null;
		if (FullscreenWidget == widget)
			FullscreenWidget = null;
		if (CurrentWidget == widget)
			CurrentWidget = null;
		if (widget.Dock != null)
			Remove(widget);
		Widgets.Remove(widget);
		widget.Workspace = null;
		return widget;
	}

	/// <summary>The window is discarded: every widget's view model goes with it.</summary>
	public void DisposeWidgets()
	{
		foreach (var widget in Widgets)
			(widget.Content as IDisposable)?.Dispose();
	}

	/// <summary>
	/// Hotkey: collapses an expanded side widget, otherwise shows the widget. A multi-instance
	/// widget (chart) always opens a new one in the current view.
	/// </summary>
	public void Toggle(WorkspaceTool tool)
	{
		if (WorkspaceHelpers.AllowsMultipleInstances(tool))
		{
			Spawn(tool);
			return;
		}
		var active = new[] { LeftActive, RightActive, BottomActive }.FirstOrDefault(x =>
			x?.Tool == tool
		);
		if (active != null)
			Collapse(active);
		else
			Show(tool);
	}

	public void ToggleSide(WidgetItem widget)
	{
		if (widget.IsActive)
			Collapse(widget);
		else
			Reveal(widget);
	}

	public void Collapse(WidgetItem widget)
	{
		if (LeftActive == widget)
			LeftActive = null;
		if (RightActive == widget)
			RightActive = null;
		if (BottomActive == widget)
			BottomActive = null;
	}

	/// <summary>
	/// Drag and drop result. Center: placed on <paramref name="edge"/> of the widget
	/// <paramref name="targetId"/> (no target: right of everything) in the shown view, or in view
	/// <paramref name="viewId"/> (dropped on its tab), which is then shown. Strips (left, right,
	/// bottom): inserted at icon position <paramref name="index"/> (null appends).
	/// </summary>
	public void Drop(
		WidgetItem widget,
		WidgetDock dock,
		int? index = null,
		string? targetId = null,
		DropEdge edge = DropEdge.Right,
		string? viewId = null
	)
	{
		if (dock == WidgetDock.Center)
		{
			var view =
				(viewId is null ? null : Views.FirstOrDefault(x => x.Id == viewId)) ?? SelectedView;
			if (targetId == widget.Id)
				return;
			var from = widget.IsCenter ? FindView(widget.Id) : null;
			// Dropped on the tab of the view it is already in: it stays where it is
			if (viewId != null && from == view)
			{
				SelectedView = view;
				return;
			}
			if (from != null)
				from.Root = CenterLayoutHelpers.Remove(from.Root, widget.Id);
			else
			{
				if (widget.Dock != null)
					Remove(widget);
				SetDock(widget, WidgetDock.Center);
			}
			view.Root = CenterLayoutHelpers.Insert(view.Root, widget.Id, targetId, edge);
			SelectedView = view;
			RaiseCenterChanged();
			return;
		}

		if (widget.Dock == dock && dock != WidgetDock.Floating)
		{
			// Reorder within the same strip
			var host = GetSideHost(dock);
			var from = host.IndexOf(widget);
			var to = index ?? host.Count;
			// Inserting after itself shifts by one once the widget is taken out
			if (to > from)
				to--;
			if (to != from)
				host.Move(from, Math.Clamp(to, 0, host.Count - 1));
		}
		else if (widget.Dock != dock)
		{
			if (widget.Dock != null)
				Remove(widget);
			Place(widget, dock, index);
		}
		Reveal(widget);
	}

	/// <summary>Closes the widget; extra instances (a second chart) are discarded.</summary>
	public void Close(WidgetItem widget)
	{
		if (widget.Dock != null)
			Remove(widget);
		(widget.Content as IWidgetOpenState)?.SetOpen(false);
		if (!WorkspaceHelpers.AllowsMultipleInstances(widget.Tool))
			return;
		Widgets.Remove(widget);
		(widget.Content as IDisposable)?.Dispose();
	}

	public void FillLayout(WorkspaceLayoutDto layout)
	{
		layout.LeftWidth = LeftWidth;
		layout.RightWidth = RightWidth;
		layout.BottomHeight = BottomHeight;
		layout.LeftWidgetId = LeftActive?.Id;
		layout.RightWidgetId = RightActive?.Id;
		layout.BottomWidgetId = BottomActive?.Id;
		layout.Views = Views
			.Select(x => new CenterViewDto
			{
				Id = x.Id,
				Name = x.Name,
				Root = x.Root,
			})
			.ToList();
		layout.SelectedViewId = SelectedView.Id;
		layout.Center = null;
		layout.Widgets = Views
			.SelectMany(x => CenterLayoutHelpers.GetWidgetIds(x.Root))
			.Select(Find)
			.OfType<WidgetItem>()
			.Concat(Left)
			.Concat(Right)
			.Concat(Bottom)
			.Concat(Floating)
			.Select(SavePlacement)
			.ToList();
	}

	// A widget's saved placement: where it is and its own settings (symbol, link group, …)
	private static WidgetPlacementDto SavePlacement(WidgetItem widget)
	{
		var placement = new WidgetPlacementDto
		{
			Id = widget.Id,
			Tool = widget.Tool,
			Dock = widget.Dock!.Value,
			FloatX = widget.FloatX,
			FloatY = widget.FloatY,
			FloatWidth = widget.FloatWidth,
			FloatHeight = widget.FloatHeight,
			IsPinned = widget.IsPinned,
			SideWidth = widget.SideWidth,
			BottomHeight = widget.BottomHeight,
		};
		if (widget.Content is ILinkable linkable)
			placement.LinkGroup = linkable.Link.Group;
		(widget.Content as IWidgetState)?.SaveState(placement);
		return placement;
	}

	// Everything of a saved placement but its dock, which the caller sets
	private static void LoadPlacement(WidgetItem widget, WidgetPlacementDto placement)
	{
		widget.FloatX = placement.FloatX;
		widget.FloatY = placement.FloatY;
		widget.FloatWidth = placement.FloatWidth;
		widget.FloatHeight = placement.FloatHeight;
		widget.IsPinned = placement.IsPinned;
		widget.SideWidth = placement.SideWidth;
		widget.BottomHeight = placement.BottomHeight;
		if (widget.Content is ILinkable linkable)
			linkable.Link.Group = placement.LinkGroup;
		(widget.Content as IWidgetState)?.LoadState(placement);
	}

	// Adopts a single-instance widget released by another workspace; it is still closed here
	private WidgetItem? Claim(WorkspaceTool tool)
	{
		if (_claim?.Invoke(tool) is not { } widget)
			return null;
		widget.Workspace = this;
		Widgets.Add(widget);
		return widget;
	}

	private WidgetItem CreateItem(string id, WorkspaceTool tool)
	{
		var widget = new WidgetItem(id, tool, _createContent(tool)) { Workspace = this };
		Widgets.Add(widget);
		return widget;
	}

	private string NewId(WorkspaceTool tool) =>
		WorkspaceHelpers.GetFreeId(
			WorkspaceHelpers.GetDefaultId(tool),
			Widgets.Select(x => x.Id).ToHashSet()
		);

	private WidgetDock GetSpawnDock(WorkspaceTool tool) =>
		_lastDock.TryGetValue(tool, out var dock) ? dock : WorkspaceHelpers.GetDefaultDock(tool);

	private ObservableCollection<WidgetItem> GetSideHost(WidgetDock dock) =>
		dock switch
		{
			WidgetDock.Left => Left,
			WidgetDock.Right => Right,
			WidgetDock.Bottom => Bottom,
			_ => Floating,
		};

	private void SetDock(WidgetItem widget, WidgetDock? dock)
	{
		// Moving or closing a fullscreen widget brings the normal layout back first
		if (FullscreenWidget == widget)
			FullscreenWidget = null;
		if (dock is null && CurrentWidget == widget)
			CurrentWidget = null;
		widget.Dock = dock;
		if (dock is not { } value)
			return;
		widget.LastDock = value;
		_lastDock[widget.Tool] = value;
	}

	private void Place(WidgetItem widget, WidgetDock dock, int? index = null)
	{
		SetDock(widget, dock);
		if (dock == WidgetDock.Center)
		{
			SelectedView.Root = CenterLayoutHelpers.Insert(SelectedView.Root, widget.Id);
			RaiseCenterChanged();
			return;
		}
		var host = GetSideHost(dock);
		if (index is { } i && i >= 0 && i <= host.Count)
			host.Insert(i, widget);
		else
			host.Add(widget);
	}

	private void Remove(WidgetItem widget)
	{
		Collapse(widget);
		if (widget.IsCenter)
		{
			if (FindView(widget.Id) is { } view)
				view.Root = CenterLayoutHelpers.Remove(view.Root, widget.Id);
			widget.Dock = null;
			RaiseCenterChanged();
			return;
		}
		GetSideHost(widget.Dock!.Value).Remove(widget);
		widget.Dock = null;
	}

	// Expanding a docked widget or raising a floating one also focuses it: it becomes current and
	// gets keyboard focus
	private void Reveal(WidgetItem widget)
	{
		switch (widget.Dock)
		{
			case WidgetDock.Left:
				LeftActive = widget;
				break;
			case WidgetDock.Right:
				RightActive = widget;
				break;
			case WidgetDock.Bottom:
				BottomActive = widget;
				break;
			case WidgetDock.Floating:
				break;
			case WidgetDock.Center:
				// A center widget in another view: show that view
				if (FindView(widget.Id) is { } view)
					SelectedView = view;
				return;
			default:
				return;
		}
		CurrentWidget = widget;
		widget.RequestFocus();
	}

	private static bool IsInView(CenterViewItem view, string widgetId) =>
		CenterLayoutHelpers.Contains(view.Root, widgetId);

	private void UpdateViewFlags()
	{
		for (var i = 0; i < Views.Count; i++)
		{
			Views[i].CanClose = Views.Count > 1;
			Views[i].CanMoveLeft = i > 0;
			Views[i].CanMoveRight = i < Views.Count - 1;
		}
	}

	private void RaiseCenterChanged()
	{
		OnPropertyChanged(nameof(HasCenterWidgets));
		CenterChanged?.Invoke();
	}

	private static void Activate(WidgetItem? oldValue, WidgetItem? newValue)
	{
		oldValue?.IsActive = false;
		newValue?.IsActive = true;
	}
}
