using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using LT.App.ViewModels;
using LT.Contracts.Dtos.Settings;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;

namespace LT.App.Workspace;

/// <summary>
/// Where every widget instance lives: in the center split layout, on a collapsible side strip,
/// or in its own floating window. Widgets are spawned, moved and closed independently; charts can
/// be spawned several times.
/// </summary>
public partial class WorkspaceViewModel : ViewModelBase
{
	private readonly Func<WorkspaceTool, object> _createContent;

	// Last dock per widget kind, for spawning new instances where the previous one was
	private readonly Dictionary<WorkspaceTool, WidgetDock> _lastDock = [];

	public WorkspaceViewModel(Func<WorkspaceTool, object> createContent, LayoutDto layout)
	{
		_createContent = createContent;
		LeftWidth = layout.LeftWidth;
		RightWidth = layout.RightWidth;

		var centerIds = new List<string>();
		foreach (var placement in WorkspaceHelpers.Normalize(layout.Widgets))
		{
			var widget = CreateItem(placement.Id, placement.Tool);
			widget.FloatX = placement.FloatX;
			widget.FloatY = placement.FloatY;
			widget.FloatWidth = placement.FloatWidth;
			widget.FloatHeight = placement.FloatHeight;
			widget.IsPinned = placement.IsPinned;
			if (widget.Content is ILinkable linkable)
				linkable.Link.Group = placement.LinkGroup;
			(widget.Content as IWidgetState)?.LoadState(placement);
			if (placement.Dock == WidgetDock.Center)
			{
				SetDock(widget, WidgetDock.Center);
				centerIds.Add(widget.Id);
			}
			else
				Place(widget, placement.Dock);
		}
		CenterRoot = CenterLayoutHelpers.Normalize(layout.Center, centerIds);

		LeftActive = Left.FirstOrDefault(x => x.Id == layout.LeftWidgetId);
		RightActive = Right.FirstOrDefault(x => x.Id == layout.RightWidgetId);
	}

	/// <summary>Every instance that exists: open ones and closed single-instance widgets.</summary>
	public ObservableCollection<WidgetItem> Widgets { get; } = [];

	public ObservableCollection<WidgetItem> Left { get; } = [];
	public ObservableCollection<WidgetItem> Right { get; } = [];
	public ObservableCollection<WidgetItem> Floating { get; } = [];

	/// <summary>Center split tree; null when the center is empty. See <see cref="CenterChanged"/>.</summary>
	public CenterNodeDto? CenterRoot { get; private set; }

	/// <summary>Raised whenever the center tree changes shape.</summary>
	public event Action? CenterChanged;

	public bool HasCenterWidgets => CenterRoot != null;

	/// <summary>Expanded widget of each side, null when that side panel is collapsed.</summary>
	[ObservableProperty]
	public partial WidgetItem? LeftActive { get; set; }

	[ObservableProperty]
	public partial WidgetItem? RightActive { get; set; }

	/// <summary>Side panel widths; the view applies them and writes them back before saving.</summary>
	public double LeftWidth { get; set; }
	public double RightWidth { get; set; }

	partial void OnLeftActiveChanged(WidgetItem? oldValue, WidgetItem? newValue) =>
		Activate(oldValue, newValue);

	partial void OnRightActiveChanged(WidgetItem? oldValue, WidgetItem? newValue) =>
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
	public void Show(WorkspaceTool tool)
	{
		var widget =
			Widgets.FirstOrDefault(x => x.Tool == tool && x.IsOpen)
			?? Widgets.FirstOrDefault(x => x.Tool == tool);
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
		var widget = CreateItem(NewId(tool), tool);
		Place(widget, GetSpawnDock(tool));
		Reveal(widget);
	}

	/// <summary>Hotkey: collapses an expanded side widget, otherwise shows the widget.</summary>
	public void Toggle(WorkspaceTool tool)
	{
		var active = new[] { LeftActive, RightActive }.FirstOrDefault(x => x?.Tool == tool);
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
	}

	/// <summary>
	/// Drag and drop result. Center: placed on <paramref name="edge"/> of the widget
	/// <paramref name="targetId"/> (no target: right of everything). Sides: inserted at icon
	/// position <paramref name="index"/> (null appends).
	/// </summary>
	public void Drop(
		WidgetItem widget,
		WidgetDock dock,
		int? index = null,
		string? targetId = null,
		DropEdge edge = DropEdge.Right
	)
	{
		if (dock == WidgetDock.Center)
		{
			if (targetId == widget.Id)
				return;
			if (widget.IsCenter)
				CenterRoot = CenterLayoutHelpers.Remove(CenterRoot, widget.Id);
			else
			{
				if (widget.Dock != null)
					Remove(widget);
				SetDock(widget, WidgetDock.Center);
			}
			CenterRoot = CenterLayoutHelpers.Insert(CenterRoot, widget.Id, targetId, edge);
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
		if (!WorkspaceHelpers.AllowsMultipleInstances(widget.Tool))
			return;
		Widgets.Remove(widget);
		(widget.Content as IDisposable)?.Dispose();
	}

	public void FillLayout(LayoutDto layout)
	{
		layout.LeftWidth = LeftWidth;
		layout.RightWidth = RightWidth;
		layout.LeftWidgetId = LeftActive?.Id;
		layout.RightWidgetId = RightActive?.Id;
		layout.Center = CenterRoot;
		layout.Widgets = CenterLayoutHelpers
			.GetWidgetIds(CenterRoot)
			.Select(Find)
			.OfType<WidgetItem>()
			.Concat(Left)
			.Concat(Right)
			.Concat(Floating)
			.Select(x =>
			{
				var placement = new WidgetPlacementDto
				{
					Id = x.Id,
					Tool = x.Tool,
					Dock = x.Dock!.Value,
					FloatX = x.FloatX,
					FloatY = x.FloatY,
					FloatWidth = x.FloatWidth,
					FloatHeight = x.FloatHeight,
					IsPinned = x.IsPinned,
				};
				if (x.Content is ILinkable linkable)
					placement.LinkGroup = linkable.Link.Group;
				(x.Content as IWidgetState)?.SaveState(placement);
				return placement;
			})
			.ToList();
	}

	private WidgetItem CreateItem(string id, WorkspaceTool tool)
	{
		var widget = new WidgetItem(id, tool, _createContent(tool)) { Workspace = this };
		Widgets.Add(widget);
		return widget;
	}

	private string NewId(WorkspaceTool tool)
	{
		var id = WorkspaceHelpers.GetDefaultId(tool);
		return Widgets.Any(x => x.Id == id) ? $"{id}-{Guid.NewGuid():N}"[..(id.Length + 9)] : id;
	}

	private WidgetDock GetSpawnDock(WorkspaceTool tool) =>
		_lastDock.TryGetValue(tool, out var dock) ? dock : WorkspaceHelpers.GetDefaultDock(tool);

	private ObservableCollection<WidgetItem> GetSideHost(WidgetDock dock) =>
		dock switch
		{
			WidgetDock.Left => Left,
			WidgetDock.Right => Right,
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
			CenterRoot = CenterLayoutHelpers.Insert(CenterRoot, widget.Id);
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
			CenterRoot = CenterLayoutHelpers.Remove(CenterRoot, widget.Id);
			widget.Dock = null;
			RaiseCenterChanged();
			return;
		}
		GetSideHost(widget.Dock!.Value).Remove(widget);
		widget.Dock = null;
	}

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
			case WidgetDock.Floating:
				widget.RequestFocus();
				break;
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
