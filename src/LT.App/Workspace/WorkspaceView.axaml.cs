using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Settings;
using LT.Contracts.Enums;

namespace LT.App.Workspace;

/// <summary>
/// Hosts widget frames: the expanded side widgets, the center split layout and the floating
/// windows. Each frame is created once per widget instance and moved between hosts, so a widget
/// keeps its state.
/// </summary>
public partial class WorkspaceView : UserControl
{
	private const int LeftColumn = 1;
	private const int RightColumn = 5;
	private const double MinPanelWidth = 220;
	private const double SplitterSize = 6;

	private readonly Dictionary<string, WidgetFrame> _frames = [];
	private readonly Dictionary<string, FloatingWidgetWindow> _windows = [];
	private readonly HashSet<string> _opening = [];

	// Split node → the grid built for it, to read splitter-set sizes back into the tree
	private readonly Dictionary<CenterNodeDto, Grid> _splitGrids = [];
	private WorkspaceViewModel? _viewModel;

	// The widget whose frame sits in the window's fullscreen layer (or whose window is maximized)
	private WidgetItem? _fullscreen;

	public WorkspaceView() => InitializeComponent();

	/// <summary>View of the first open instance of a widget (e.g. to focus the watchlist search).</summary>
	public Control? GetWidgetView(WorkspaceTool tool) =>
		_viewModel?.Widgets.FirstOrDefault(x => x.Tool == tool && x.IsOpen) is { } widget
		&& _frames.TryGetValue(widget.Id, out var frame)
			? frame.Body
			: null;

	/// <summary>Copies panel sizes and floating window bounds into the model before saving.</summary>
	public void StoreLayout()
	{
		if (_viewModel is null)
			return;
		if (_viewModel.LeftActive is { } left)
			StoreSideWidth(left, true, LayoutGrid.ColumnDefinitions[LeftColumn].ActualWidth);
		if (_viewModel.RightActive is { } right)
			StoreSideWidth(right, false, LayoutGrid.ColumnDefinitions[RightColumn].ActualWidth);
		StoreCenterSizes();
		foreach (var (id, window) in _windows)
			if (_viewModel.Find(id) is { } widget)
				StoreBounds(widget, window);
	}

	protected override void OnDataContextChanged(EventArgs e)
	{
		base.OnDataContextChanged(e);
		if (_viewModel != null)
		{
			_viewModel.PropertyChanged -= OnViewModelPropertyChanged;
			_viewModel.CenterChanged -= RebuildCenter;
			_viewModel.Floating.CollectionChanged -= OnFloatingChanged;
			_viewModel.Widgets.CollectionChanged -= OnWidgetsChanged;
			foreach (var widget in _viewModel.Widgets)
				widget.FocusRequested -= OnFocusRequested;
		}
		_viewModel = DataContext as WorkspaceViewModel;
		if (_viewModel is null)
			return;
		_viewModel.PropertyChanged += OnViewModelPropertyChanged;
		_viewModel.CenterChanged += RebuildCenter;
		_viewModel.Floating.CollectionChanged += OnFloatingChanged;
		_viewModel.Widgets.CollectionChanged += OnWidgetsChanged;
		foreach (var widget in _viewModel.Widgets)
			widget.FocusRequested += OnFocusRequested;

		ApplySide(_viewModel.LeftActive, LeftHost, LeftColumn, LeftSplitter, true);
		ApplySide(_viewModel.RightActive, RightHost, RightColumn, RightSplitter, false);
		RebuildCenter();
	}

	// Floating windows need the main window as owner, which exists only once attached
	protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
	{
		base.OnAttachedToVisualTree(e);
		SyncFloating();
		// Unpinned side widgets collapse when a click or focus lands anywhere else in the window
		if (TopLevel.GetTopLevel(this) is { } window)
		{
			window.AddHandler(
				PointerPressedEvent,
				OnWindowInput,
				RoutingStrategies.Tunnel,
				handledEventsToo: true
			);
			window.AddHandler(GotFocusEvent, OnWindowInput, RoutingStrategies.Tunnel);
		}
	}

	protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
	{
		if (TopLevel.GetTopLevel(this) is { } window)
		{
			window.RemoveHandler(PointerPressedEvent, OnWindowInput);
			window.RemoveHandler(GotFocusEvent, OnWindowInput);
		}
		base.OnDetachedFromVisualTree(e);
	}

	private void OnWindowInput(object? sender, RoutedEventArgs e) =>
		CollapseUnpinnedOutside(e.Source as Visual);

	// Input inside a side panel, its strip or its splitter keeps it open; anything else (another
	// widget, the toolbar, a floating window: source null) collapses an unpinned one
	private void CollapseUnpinnedOutside(Visual? source)
	{
		if (_viewModel is null)
			return;
		CollapseIfOutside(_viewModel.LeftActive, source, LeftHost, LeftStrip, LeftSplitter);
		CollapseIfOutside(_viewModel.RightActive, source, RightHost, RightStrip, RightSplitter);
	}

	private void CollapseIfOutside(WidgetItem? active, Visual? source, params Visual[] own)
	{
		if (active is not { IsPinned: false, IsFullscreen: false })
			return;
		if (source != null && own.Any(x => x == source || x.IsVisualAncestorOf(source)))
			return;
		_viewModel!.Collapse(active);
	}

	private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		switch (e.PropertyName)
		{
			case nameof(WorkspaceViewModel.LeftActive):
				ApplySide(_viewModel!.LeftActive, LeftHost, LeftColumn, LeftSplitter, true);
				break;
			case nameof(WorkspaceViewModel.RightActive):
				ApplySide(_viewModel!.RightActive, RightHost, RightColumn, RightSplitter, false);
				break;
			case nameof(WorkspaceViewModel.FullscreenWidget):
				ApplyFullscreen();
				break;
		}
	}

	private void OnFloatingChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
		SyncFloating();

	// Discarded instances (a closed second chart) drop their frame
	private void OnWidgetsChanged(object? sender, NotifyCollectionChangedEventArgs e)
	{
		foreach (var widget in e.NewItems?.OfType<WidgetItem>() ?? [])
			widget.FocusRequested += OnFocusRequested;
		foreach (var widget in e.OldItems?.OfType<WidgetItem>() ?? [])
		{
			widget.FocusRequested -= OnFocusRequested;
			if (_frames.Remove(widget.Id, out var frame))
				Detach(frame);
		}
	}

	// Raise a floating widget's window, then put keyboard focus on the widget's first focusable
	// control (after layout: a just-expanded side widget isn't arranged yet)
	private void OnFocusRequested(WidgetItem widget)
	{
		if (_windows.TryGetValue(widget.Id, out var window))
			window.Activate();
		Dispatcher.UIThread.Post(
			() =>
			{
				if (!_frames.TryGetValue(widget.Id, out var frame) || frame.Body is null)
					return;
				frame
					.Body.GetVisualDescendants()
					.OfType<InputElement>()
					.FirstOrDefault(x =>
						x.Focusable && x.IsEffectivelyVisible && x.IsEffectivelyEnabled
					)
					?.Focus();
			},
			DispatcherPriority.Loaded
		);
	}

	private WidgetFrame GetFrame(WidgetItem widget)
	{
		if (!_frames.TryGetValue(widget.Id, out var frame))
		{
			_frames[widget.Id] = frame = new WidgetFrame(widget);
			AttachDrag(frame);
			// The last widget clicked or typed in is what Ctrl+Shift+F acts on
			frame.AddHandler(
				PointerPressedEvent,
				(_, _) => SetCurrent(widget),
				RoutingStrategies.Tunnel,
				handledEventsToo: true
			);
			frame.GotFocus += (_, _) => SetCurrent(widget);
		}
		return frame;
	}

	// A control can have one parent; take the frame out of wherever it sits before re-hosting it
	private static void Detach(Control frame)
	{
		switch (frame.Parent)
		{
			case Panel panel:
				panel.Children.Remove(frame);
				break;
			case Decorator decorator:
				decorator.Child = null;
				break;
			case ContentControl contentControl:
				contentControl.Content = null;
				break;
		}
	}

	// A collapsed side keeps its splitter column as the gap; the width is kept for reopening
	private void ApplySide(
		WidgetItem? active,
		Border host,
		int column,
		GridSplitter splitter,
		bool isLeft
	)
	{
		var definition = LayoutGrid.ColumnDefinitions[column];
		// The widget leaving the panel keeps its width for the next time it opens
		if (host.Child is { } current && current.DataContext != active)
		{
			if (current.DataContext is WidgetItem previous)
				StoreSideWidth(previous, isLeft, definition.ActualWidth);
			host.Child = null;
		}

		if (active is null)
		{
			definition.Width = new GridLength(0);
			splitter.IsVisible = false;
			return;
		}

		var frame = GetFrame(active);
		if (host.Child != frame && active != _fullscreen)
		{
			Detach(frame);
			host.Child = frame;
		}
		var width = active.SideWidth ?? (isLeft ? _viewModel!.LeftWidth : _viewModel!.RightWidth);
		definition.Width = new GridLength(Math.Max(MinPanelWidth, width));
		splitter.IsVisible = true;
	}

	private void SetCurrent(WidgetItem widget)
	{
		if (_viewModel != null)
			_viewModel.CurrentWidget = widget;
	}

	// Floating widgets maximize their own window; the others move into the main window's layer
	private void ApplyFullscreen()
	{
		var next = _viewModel?.FullscreenWidget;
		if (next == _fullscreen)
			return;
		var previous = _fullscreen;
		_fullscreen = next;
		if (previous != null)
			Restore(previous);
		if (next is null)
			return;

		if (_windows.TryGetValue(next.Id, out var window))
		{
			window.WindowState = WindowState.Maximized;
			return;
		}
		var frame = GetFrame(next);
		Detach(frame);
		(TopLevel.GetTopLevel(this) as IFullscreenHost)?.ShowFullscreen(frame);
	}

	private void Restore(WidgetItem widget)
	{
		if (_windows.TryGetValue(widget.Id, out var window))
		{
			window.WindowState = WindowState.Normal;
			return;
		}
		(TopLevel.GetTopLevel(this) as IFullscreenHost)?.ShowFullscreen(null);
		if (widget.IsCenter)
			RebuildCenter();
		else if (widget == _viewModel?.LeftActive)
			ApplySide(widget, LeftHost, LeftColumn, LeftSplitter, true);
		else if (widget == _viewModel?.RightActive)
			ApplySide(widget, RightHost, RightColumn, RightSplitter, false);
	}

	// Each side widget remembers its own width; the side remembers the last one for newcomers
	private void StoreSideWidth(WidgetItem widget, bool isLeft, double width)
	{
		if (width <= 0)
			return;
		widget.SideWidth = width;
		if (isLeft)
			_viewModel!.LeftWidth = width;
		else
			_viewModel!.RightWidth = width;
	}

	/// <summary>Rebuilds the center from the split tree: nested grids with a splitter between children.</summary>
	private void RebuildCenter()
	{
		foreach (var grid in _splitGrids.Values)
			grid.Children.Clear();
		_splitGrids.Clear();
		CenterContent.Child = null;

		if (_viewModel?.CenterRoot is { } root)
			CenterContent.Child = Build(root);
	}

	private Control Build(CenterNodeDto node)
	{
		if (node.IsLeaf)
		{
			var widget = _viewModel!.Find(node.WidgetId!);
			// A fullscreen widget keeps its cell empty until it is restored
			if (widget is null || widget == _fullscreen)
				return new Panel();
			var frame = GetFrame(widget);
			Detach(frame);
			return frame;
		}

		var horizontal = node.Orientation == SplitOrientation.Horizontal;
		var grid = new Grid();
		_splitGrids[node] = grid;
		for (var i = 0; i < node.Children.Count; i++)
		{
			var child = node.Children[i];
			if (i > 0)
			{
				AddDefinition(grid, horizontal, new GridLength(SplitterSize));
				var splitter = new GridSplitter
				{
					ResizeDirection = horizontal
						? GridResizeDirection.Columns
						: GridResizeDirection.Rows,
					Background = Brushes.Transparent,
				};
				// Sizes are read back right after a resize: by the next rebuild the tree may
				// already have changed shape
				splitter.DragCompleted += (_, _) => StoreCenterSizes();
				SetCell(splitter, horizontal, i * 2 - 1);
				grid.Children.Add(splitter);
			}
			AddDefinition(grid, horizontal, new GridLength(child.Size, GridUnitType.Star));
			var content = Build(child);
			SetCell(content, horizontal, i * 2);
			grid.Children.Add(content);
		}
		return grid;
	}

	private static void AddDefinition(Grid grid, bool horizontal, GridLength length)
	{
		if (horizontal)
			grid.ColumnDefinitions.Add(new ColumnDefinition(length));
		else
			grid.RowDefinitions.Add(new RowDefinition(length));
	}

	private static void SetCell(Control control, bool horizontal, int index)
	{
		if (horizontal)
			Grid.SetColumn(control, index);
		else
			Grid.SetRow(control, index);
	}

	// Actual pixel sizes become the children's star weights, so proportions survive rebuilds
	private void StoreCenterSizes()
	{
		foreach (var (node, grid) in _splitGrids)
		{
			var horizontal = node.Orientation == SplitOrientation.Horizontal;
			for (var i = 0; i < node.Children.Count; i++)
			{
				var index = i * 2;
				var size = horizontal
					? grid.ColumnDefinitions.ElementAtOrDefault(index)?.ActualWidth ?? 0
					: grid.RowDefinitions.ElementAtOrDefault(index)?.ActualHeight ?? 0;
				if (size > 0)
					node.Children[i].Size = size;
			}
		}
	}

	private void SyncFloating()
	{
		if (_viewModel is null || TopLevel.GetTopLevel(this) is not Window owner)
			return;

		foreach (var (id, window) in _windows.ToList())
		{
			var widget = _viewModel.Find(id);
			if (widget?.IsFloating == true)
				continue;
			// Unregister first so the Closing handler knows this isn't the user closing it
			_windows.Remove(id);
			if (widget != null)
				StoreBounds(widget, window);
			window.Content = null;
			window.Close();
		}

		var cascade = 0;
		foreach (var widget in _viewModel.Floating)
		{
			if (_windows.ContainsKey(widget.Id) || _opening.Contains(widget.Id))
				continue;
			// Detach now, show in the new window after a layout pass: moving a control between
			// windows while the old one still has it queued for layout crashes Avalonia
			Detach(GetFrame(widget));
			_opening.Add(widget.Id);
			var index = cascade++;
			Dispatcher.UIThread.Post(
				() =>
				{
					_opening.Remove(widget.Id);
					if (widget.IsFloating && !_windows.ContainsKey(widget.Id))
						OpenWindow(widget, owner, index);
				},
				DispatcherPriority.Background
			);
		}
	}

	private void OpenWindow(WidgetItem widget, Window owner, int cascade)
	{
		var frame = GetFrame(widget);
		Detach(frame);
		var window = new FloatingWidgetWindow
		{
			Title = widget.Title,
			Width = widget.FloatWidth ?? WorkspaceConstants.FloatingDefaultWidth,
			Height = widget.FloatHeight ?? WorkspaceConstants.FloatingDefaultHeight,
			MinWidth = WorkspaceConstants.FloatingMinWidth,
			MinHeight = WorkspaceConstants.FloatingMinHeight,
			WindowStartupLocation = WindowStartupLocation.Manual,
			Content = frame,
		};
		if (widget is { FloatX: { } x, FloatY: { } y })
			window.Position = new PixelPoint((int)x, (int)y);
		else
		{
			var offset = (int)(WorkspaceConstants.FloatingCascadeOffset * (cascade + 1));
			window.Position = owner.Position + new PixelPoint(offset * 2, offset * 2);
		}

		window.Closing += (_, e) =>
		{
			StoreBounds(widget, window);
			// The user closed the window (not us, not the main window closing): close the widget
			if (
				_windows.GetValueOrDefault(widget.Id) == window
				&& e.CloseReason == WindowCloseReason.WindowClosing
				&& !e.IsProgrammatic
			)
			{
				_windows.Remove(widget.Id);
				_viewModel?.Close(widget);
			}
		};
		window.Closed += (_, _) =>
		{
			if (window.Content == frame)
				window.Content = null;
		};

		window.AddHandler(
			PointerPressedEvent,
			(_, _) => CollapseUnpinnedOutside(null),
			RoutingStrategies.Tunnel,
			handledEventsToo: true
		);
		_windows[widget.Id] = window;
		window.Show(owner);
	}

	private static void StoreBounds(WidgetItem widget, Window window)
	{
		widget.FloatX = window.Position.X;
		widget.FloatY = window.Position.Y;
		widget.FloatWidth = window.Width;
		widget.FloatHeight = window.Height;
	}
}
