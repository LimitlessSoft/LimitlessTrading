using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using LT.App.Stores;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Settings;
using LT.Contracts.Enums;

namespace LT.App.Workspace;

/// <summary>
/// Hosts widget frames: the expanded side and bottom widgets, the center split layout of every
/// view and the floating windows. Each frame is created once per widget instance and moved between
/// hosts, so a widget keeps its state. Every view has its own host, kept in the visual tree and
/// only hidden while another view is shown: switching views moves no frame (a drag in progress
/// keeps its pointer capture).
/// </summary>
public partial class WorkspaceView : UserControl
{
	private const int LeftColumn = 1;
	private const int RightColumn = 5;
	private const int BottomSplitterRow = 2;
	private const int BottomRow = 3;
	private const double MinPanelWidth = 220;
	private const double MinPanelHeight = 120;

	// Cards: splitters are 6px gaps. Flush: 1px lines, grabbed over 3px on each side.
	private const double CardsSplitterSize = 6;
	private const double FlushSplitterSize = 1;
	private const double FlushSplitterReach = 3;
	private const double CardsCenterMargin = 8;
	private const double CardsTabsGap = 6;
	private const double CardsFloatingPadding = 6;

	private readonly Dictionary<string, WidgetFrame> _frames = [];
	private readonly Dictionary<string, FloatingWidgetWindow> _windows = [];
	private readonly HashSet<string> _opening = [];

	// Split node → the grid built for it, to read splitter-set sizes back into the tree
	private readonly Dictionary<CenterNodeDto, Grid> _splitGrids = [];

	// View → the host its split tree is built into
	private readonly Dictionary<CenterViewItem, Border> _viewHosts = [];
	private WorkspaceViewModel? _viewModel;

	// The widget whose frame sits in the window's fullscreen layer (or whose window is maximized)
	private WidgetItem? _fullscreen;

	public WorkspaceView()
	{
		InitializeComponent();
		// Bubbling and unhandled only: a dropdown or a text box that cancels on Esc keeps it
		foreach (var host in new[] { LeftHost, RightHost, BottomHost })
			host.AddHandler(KeyDownEvent, OnDockedKeyDown);
	}

	/// <summary>
	/// View of the instance of a widget that showing it acts on (<see cref="WorkspaceViewModel.FindShown"/>),
	/// e.g. to focus the watchlist search.
	/// </summary>
	public Control? GetWidgetView(WorkspaceTool tool) =>
		_viewModel?.FindShown(tool) is { } widget && _frames.TryGetValue(widget.Id, out var frame)
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
		if (_viewModel.BottomActive is { } bottom)
			StoreBottomHeight(bottom, CenterColumn.RowDefinitions[BottomRow].ActualHeight);
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
			_viewModel.Appearance?.PropertyChanged -= OnAppearanceChanged;
			_viewModel.CenterChanged -= RebuildCenter;
			_viewModel.Views.CollectionChanged -= OnViewsChanged;
			_viewModel.Floating.CollectionChanged -= OnFloatingChanged;
			_viewModel.Widgets.CollectionChanged -= OnWidgetsChanged;
			foreach (var widget in _viewModel.Widgets)
				widget.FocusRequested -= OnFocusRequested;
			ClearHosts();
		}
		_viewModel = DataContext as WorkspaceViewModel;
		if (_viewModel is null)
			return;
		_viewModel.PropertyChanged += OnViewModelPropertyChanged;
		_viewModel.Appearance?.PropertyChanged += OnAppearanceChanged;
		ApplyLayoutStyle();
		_viewModel.CenterChanged += RebuildCenter;
		_viewModel.Views.CollectionChanged += OnViewsChanged;
		_viewModel.Floating.CollectionChanged += OnFloatingChanged;
		_viewModel.Widgets.CollectionChanged += OnWidgetsChanged;
		foreach (var widget in _viewModel.Widgets)
			widget.FocusRequested += OnFocusRequested;

		ApplySide(_viewModel.LeftActive, LeftHost, LeftColumn, LeftSplitter, true);
		ApplySide(_viewModel.RightActive, RightHost, RightColumn, RightSplitter, false);
		ApplyBottom(_viewModel.BottomActive);
		RebuildCenter();
		ApplyFullscreen();
		SyncFloating();
		ApplyWindowState();
	}

	/// <summary>
	/// The workspace shown here is replaced (the main window takes over an extra window's) or
	/// dropped (that extra window closes): every frame lets go of its widget view model, floating
	/// windows close (the widgets stay floating) and the hosts empty out, so the next workspace
	/// starts from nothing and its ids can't pick up a frame of the old one.
	/// </summary>
	private void ClearHosts()
	{
		EndDragFeedback();
		EndViewDragFeedback();
		if (_fullscreen != null)
		{
			_fullscreen = null;
			(TopLevel.GetTopLevel(this) as IFullscreenHost)?.ShowFullscreen(null);
		}
		foreach (var (id, window) in _windows.ToList())
		{
			// Unregistered first: the Closing handler then knows the user didn't close it
			_windows.Remove(id);
			if (_viewModel?.Find(id) is { } widget)
				StoreBounds(widget, window);
			window.Content = null;
			window.Close();
		}
		_opening.Clear();
		LeftHost.Child = null;
		RightHost.Child = null;
		BottomHost.Child = null;
		foreach (var grid in _splitGrids.Values)
			grid.Children.Clear();
		_splitGrids.Clear();
		CenterViews.Children.Clear();
		_viewHosts.Clear();
		foreach (var frame in _frames.Values)
		{
			Detach(frame);
			frame.Release();
		}
		_frames.Clear();
	}

	// Floating windows need the main window as owner, which exists only once attached
	protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
	{
		base.OnAttachedToVisualTree(e);
		Attached.Add(this);
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
			window.PropertyChanged += OnWindowPropertyChanged;
		}
		ApplyWindowState();
	}

	protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
	{
		Attached.Remove(this);
		if (TopLevel.GetTopLevel(this) is { } window)
		{
			window.RemoveHandler(PointerPressedEvent, OnWindowInput);
			window.RemoveHandler(GotFocusEvent, OnWindowInput);
			window.PropertyChanged -= OnWindowPropertyChanged;
		}
		base.OnDetachedFromVisualTree(e);
	}

	private void OnWindowPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
	{
		if (e.Property == Window.WindowStateProperty)
			ApplyWindowState();
	}

	// A minimized window shows none of its docked and center widgets: they stop their streams
	private void ApplyWindowState()
	{
		if (_viewModel != null && TopLevel.GetTopLevel(this) is Window window)
			_viewModel.IsWindowMinimized = window.WindowState == WindowState.Minimized;
	}

	private void OnWindowInput(object? sender, RoutedEventArgs e) =>
		CollapseUnpinnedOutside(e.Source as Visual);

	// Input inside a side or bottom panel, its strip or its splitter keeps it open; anything else (another
	// widget, the toolbar, a floating window: source null) collapses an unpinned one
	private void CollapseUnpinnedOutside(Visual? source)
	{
		if (_viewModel is null)
			return;
		CollapseIfOutside(_viewModel.LeftActive, source, LeftHost, LeftStrip, LeftSplitter);
		CollapseIfOutside(_viewModel.RightActive, source, RightHost, RightStrip, RightSplitter);
		CollapseIfOutside(_viewModel.BottomActive, source, BottomHost, BottomStrip, BottomSplitter);
	}

	private void CollapseIfOutside(WidgetItem? active, Visual? source, params Visual[] own)
	{
		if (active is not { IsPinned: false, IsFullscreen: false })
			return;
		if (source != null && own.Any(x => x == source || x.IsVisualAncestorOf(source)))
			return;
		_viewModel!.Collapse(active);
	}

	// Esc in an expanded side or bottom widget goes back to the center (collapsing it if unpinned)
	private void OnDockedKeyDown(object? sender, KeyEventArgs e)
	{
		if (
			e.Handled
			|| e.Key != Key.Escape
			|| e.KeyModifiers != KeyModifiers.None
			|| _viewModel is null
			|| (sender as Border)?.Child is not WidgetFrame { Widget: { } widget }
		)
			return;
		e.Handled = _viewModel.LeaveDocked(widget);
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
			case nameof(WorkspaceViewModel.BottomActive):
				ApplyBottom(_viewModel!.BottomActive);
				break;
			case nameof(WorkspaceViewModel.FullscreenWidget):
				ApplyFullscreen();
				break;
			case nameof(WorkspaceViewModel.SelectedView):
				ApplySelectedView();
				break;
		}
	}

	private bool IsFlush => _viewModel?.Appearance?.IsFlush == true;

	private double SplitterSize => IsFlush ? FlushSplitterSize : CardsSplitterSize;

	private Thickness FloatingPadding => new(IsFlush ? 0 : CardsFloatingPadding);

	private void OnAppearanceChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName != nameof(AppearanceStore.IsFlush) || _viewModel is null)
			return;
		ApplyLayoutStyle();
		ApplySide(_viewModel.LeftActive, LeftHost, LeftColumn, LeftSplitter, true);
		ApplySide(_viewModel.RightActive, RightHost, RightColumn, RightSplitter, false);
		ApplyBottom(_viewModel.BottomActive);
		RebuildCenter();
	}

	// Cards: rounded tiles with gaps around the center. Flush: no gaps, splitters are thin lines.
	// Frame corners and borders follow the window's flush class (App.axaml).
	private void ApplyLayoutStyle()
	{
		CenterHost.Margin = IsFlush
			? default
			: new Thickness(0, CardsTabsGap, 0, CardsCenterMargin);
		StyleSplitter(LeftSplitter, true);
		StyleSplitter(RightSplitter, true);
		StyleSplitter(BottomSplitter, false);
		foreach (var window in _windows.Values)
			StyleFloatingWindow(window);
	}

	private void StyleSplitter(GridSplitter splitter, bool columns)
	{
		var flush = IsFlush;
		splitter.ZIndex = flush ? 1 : 0;
		splitter.Margin =
			!flush ? default
			: columns ? new Thickness(-FlushSplitterReach, 0)
			: new Thickness(0, -FlushSplitterReach);
		var key = columns ? "FlushSplitterColumnBrush" : "FlushSplitterRowBrush";
		// Before the view is attached only the application sees the app's resources
		splitter.Background = flush
			? (this.FindResource(key) ?? Application.Current?.FindResource(key)) as IBrush
			: Brushes.Transparent;
	}

	private void StyleFloatingWindow(Window window)
	{
		window.Padding = FloatingPadding;
		window.Classes.Set("flush", IsFlush);
	}

	private void OnFloatingChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
		SyncFloating();

	// Discarded instances (a closed second chart) and widgets moved to another window drop their
	// frame; the other window builds one of its own
	private void OnWidgetsChanged(object? sender, NotifyCollectionChangedEventArgs e)
	{
		foreach (var widget in e.NewItems?.OfType<WidgetItem>() ?? [])
			widget.FocusRequested += OnFocusRequested;
		foreach (var widget in e.OldItems?.OfType<WidgetItem>() ?? [])
		{
			widget.FocusRequested -= OnFocusRequested;
			if (_frames.Remove(widget.Id, out var frame))
			{
				Detach(frame);
				frame.Release();
			}
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

		// The splitter column next to the panel: the gap in cards (kept while collapsed), a line in
		// flush (gone while collapsed)
		var splitterColumn = LayoutGrid.ColumnDefinitions[isLeft ? column + 1 : column - 1];
		if (active is null)
		{
			definition.Width = new GridLength(0);
			splitterColumn.Width = new GridLength(IsFlush ? 0 : CardsSplitterSize);
			splitter.IsVisible = false;
			return;
		}
		splitterColumn.Width = new GridLength(SplitterSize);

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

	// The bottom panel works like a side one, with heights instead of widths
	private void ApplyBottom(WidgetItem? active)
	{
		var definition = CenterColumn.RowDefinitions[BottomRow];
		if (BottomHost.Child is { } current && current.DataContext != active)
		{
			if (current.DataContext is WidgetItem previous)
				StoreBottomHeight(previous, definition.ActualHeight);
			BottomHost.Child = null;
		}

		// Unlike a side, a collapsed bottom leaves no gap: the center reaches the strip
		var splitterRow = CenterColumn.RowDefinitions[BottomSplitterRow];
		if (active is null)
		{
			definition.Height = new GridLength(0);
			splitterRow.Height = new GridLength(0);
			BottomSplitter.IsVisible = false;
			return;
		}

		var frame = GetFrame(active);
		if (BottomHost.Child != frame && active != _fullscreen)
		{
			Detach(frame);
			BottomHost.Child = frame;
		}
		var height = active.BottomHeight ?? _viewModel!.BottomHeight;
		definition.Height = new GridLength(Math.Max(MinPanelHeight, height));
		splitterRow.Height = new GridLength(SplitterSize);
		BottomSplitter.IsVisible = true;
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
		else if (widget == _viewModel?.BottomActive)
			ApplyBottom(widget);
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

	private void StoreBottomHeight(WidgetItem widget, double height)
	{
		if (height <= 0)
			return;
		widget.BottomHeight = height;
		_viewModel!.BottomHeight = height;
	}

	private void OnViewsChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
		RebuildCenter();

	/// <summary>
	/// Rebuilds every view's host from its split tree: nested grids with a splitter between
	/// children. Hosts of closed views go, new views get one.
	/// </summary>
	private void RebuildCenter()
	{
		foreach (var grid in _splitGrids.Values)
			grid.Children.Clear();
		_splitGrids.Clear();
		foreach (var host in _viewHosts.Values)
			host.Child = null;

		var views = _viewModel?.Views ?? [];
		foreach (var view in _viewHosts.Keys.Except(views).ToList())
		{
			CenterViews.Children.Remove(_viewHosts[view]);
			_viewHosts.Remove(view);
		}
		foreach (var view in views)
		{
			if (!_viewHosts.TryGetValue(view, out var host))
			{
				_viewHosts[view] = host = new Border();
				CenterViews.Children.Add(host);
			}
			if (view.Root is { } root)
				host.Child = Build(root);
		}
		ApplySelectedView();
	}

	// A press on a view tab shows it; a double-click opens its name for editing. Presses on its ✕
	// and in its name box are handled before they get here. Registered after the tab's drag, which
	// must see the press first (OnViewTabAttached).
	private void OnViewTabPressed(object? sender, PointerPressedEventArgs e)
	{
		if (
			sender is not Control { DataContext: CenterViewItem view }
			|| view.IsRenaming
			|| !e.GetCurrentPoint(sender as Visual).Properties.IsLeftButtonPressed
		)
			return;
		if (e.ClickCount >= 2)
			view.BeginRenameCommand.Execute(null);
		else
			view.SelectCommand.Execute(null);
		e.Handled = true;
	}

	// A name box opens focused with the name selected, ready to type over
	private void OnViewNameBoxPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
	{
		if (e.Property != IsVisibleProperty || sender is not TextBox { IsVisible: true } box)
			return;
		Dispatcher.UIThread.Post(
			() =>
			{
				box.Focus();
				box.SelectAll();
			},
			DispatcherPriority.Loaded
		);
	}

	private void OnViewNameBoxKeyDown(object? sender, KeyEventArgs e)
	{
		if (sender is not TextBox { DataContext: CenterViewItem view })
			return;
		switch (e.Key)
		{
			case Key.Enter:
				view.CommitRenameCommand.Execute(null);
				e.Handled = true;
				break;
			case Key.Escape:
				view.CancelRenameCommand.Execute(null);
				e.Handled = true;
				break;
		}
	}

	private void OnViewNameBoxLostFocus(object? sender, RoutedEventArgs e)
	{
		if (sender is TextBox { DataContext: CenterViewItem view })
			view.CommitRenameCommand.Execute(null);
	}

	// Only the shown view's host is visible; the others stay in the visual tree, hidden
	private void ApplySelectedView()
	{
		foreach (var (view, host) in _viewHosts)
			host.IsVisible = view == _viewModel?.SelectedView;
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
				};
				StyleSplitter(splitter, horizontal);
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
					// Still ours: it may have moved to another window in the meantime
					if (
						widget.IsFloating
						&& _viewModel?.Find(widget.Id) == widget
						&& !_windows.ContainsKey(widget.Id)
					)
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
			Padding = FloatingPadding,
			Title = widget.Title,
			MinWidth = WorkspaceConstants.FloatingMinWidth,
			MinHeight = WorkspaceConstants.FloatingMinHeight,
			WindowStartupLocation = WindowStartupLocation.Manual,
			Content = frame,
		};
		// Back where it was (on its screen, maximized or not); a new one cascades from the owner
		var offset = (int)(WorkspaceConstants.FloatingCascadeOffset * (cascade + 1));
		window.Placement.Restore(
			new WindowPlacementDto
			{
				X = widget.FloatX,
				Y = widget.FloatY,
				Width = widget.FloatWidth ?? WorkspaceConstants.FloatingDefaultWidth,
				Height = widget.FloatHeight ?? WorkspaceConstants.FloatingDefaultHeight,
				State = widget.FloatState ?? WindowPlacementState.Normal,
				Screen = widget.FloatScreen,
			},
			owner.Position + new PixelPoint(offset * 2, offset * 2)
		);

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
			widget.IsFloatMinimized = false;
		};
		// A minimized floating window hides its widget, which stops its streams
		window.PropertyChanged += (_, e) =>
		{
			if (e.Property == Window.WindowStateProperty)
				widget.IsFloatMinimized = window.WindowState == WindowState.Minimized;
		};

		window.Classes.Set("flush", IsFlush);
		window.AddHandler(
			PointerPressedEvent,
			(_, _) => CollapseUnpinnedOutside(null),
			RoutingStrategies.Tunnel,
			handledEventsToo: true
		);
		_windows[widget.Id] = window;
		window.Show(owner);
	}

	// Normal bounds (position in device pixels, size in DIPs), state and screen
	private static void StoreBounds(WidgetItem widget, FloatingWidgetWindow window)
	{
		var placement = window.Placement.Capture();
		widget.FloatX = placement.X;
		widget.FloatY = placement.Y;
		widget.FloatWidth = placement.Width;
		widget.FloatHeight = placement.Height;
		widget.FloatState = placement.State;
		widget.FloatScreen = placement.Screen;
	}
}
