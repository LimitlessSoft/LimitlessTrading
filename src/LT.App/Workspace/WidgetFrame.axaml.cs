using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace LT.App.Workspace;

public partial class WidgetFrame : UserControl
{
	// Movement (DIPs) before a press on the header turns into a drag
	private const double DragThreshold = 6;

	private Point? _pressPoint;
	private bool _isDragging;

	public WidgetFrame()
	{
		InitializeComponent();
		Header.PointerPressed += OnHeaderPointerPressed;
		Header.PointerMoved += OnHeaderPointerMoved;
		Header.PointerReleased += OnHeaderPointerReleased;
		Header.PointerCaptureLost += OnHeaderPointerCaptureLost;
	}

	public WidgetFrame(WidgetItem widget)
		: this()
	{
		DataContext = widget;
		Body = WidgetViewFactory.Create(widget);
		BodyHost.Content = Body;
		if (WidgetViewFactory.CreateTitle(widget) is { } title)
			TitleHost.Content = title;
	}

	/// <summary>The widget's own view (WatchlistView, ChartView, …).</summary>
	public Control? Body { get; }

	public WidgetItem? Widget => DataContext as WidgetItem;

	/// <summary>Where on the frame the header was grabbed, so a floating window lands under the pointer.</summary>
	public Point GrabOffset { get; private set; }

	// Screen positions (device pixels): the drag can cross windows, e.g. from a floating one
	public event Action<WidgetFrame, PixelPoint>? DragStarted;
	public event Action<WidgetFrame, PixelPoint>? DragMoved;
	public event Action<WidgetFrame, PixelPoint>? DragDropped;
	public event Action<WidgetFrame>? DragCancelled;

	private void OnHeaderPointerPressed(object? sender, PointerPressedEventArgs e)
	{
		if (!e.GetCurrentPoint(Header).Properties.IsLeftButtonPressed)
			return;
		_pressPoint = e.GetPosition(this);
		e.Pointer.Capture(Header);
	}

	private void OnHeaderPointerMoved(object? sender, PointerEventArgs e)
	{
		if (_pressPoint is not { } press)
			return;
		var position = e.GetPosition(this);
		if (!_isDragging)
		{
			var delta = position - press;
			if (Math.Abs(delta.X) < DragThreshold && Math.Abs(delta.Y) < DragThreshold)
				return;
			_isDragging = true;
			GrabOffset = press;
			Header.Cursor = new Cursor(StandardCursorType.DragMove);
			DragStarted?.Invoke(this, this.PointToScreen(position));
		}
		DragMoved?.Invoke(this, this.PointToScreen(position));
	}

	private void OnHeaderPointerReleased(object? sender, PointerReleasedEventArgs e)
	{
		var wasDragging = _isDragging;
		Reset();
		e.Pointer.Capture(null);
		if (wasDragging)
			DragDropped?.Invoke(this, this.PointToScreen(e.GetPosition(this)));
	}

	private void OnHeaderPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
	{
		var wasDragging = _isDragging;
		Reset();
		if (wasDragging)
			DragCancelled?.Invoke(this);
	}

	private void Reset()
	{
		_pressPoint = null;
		_isDragging = false;
		Header.Cursor = null;
	}
}
