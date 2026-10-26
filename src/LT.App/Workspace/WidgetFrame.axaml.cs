using Avalonia;
using Avalonia.Controls;

namespace LT.App.Workspace;

public partial class WidgetFrame : UserControl
{
	private readonly DragGesture _drag;

	public WidgetFrame()
	{
		InitializeComponent();
		// A fullscreen widget stays put: restore it before moving it
		_drag = new DragGesture(Header, canStart: () => Widget?.IsFullscreen != true);
		_drag.Started += screen => DragStarted?.Invoke(this, screen);
		_drag.Moved += screen => DragMoved?.Invoke(this, screen);
		_drag.Dropped += screen => DragDropped?.Invoke(this, screen);
		_drag.Cancelled += () => DragCancelled?.Invoke(this);
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

	/// <summary>
	/// The frame is dropped (its widget was discarded or moved to another window): unbinds the
	/// widget's view and header title, which another frame may now show the view model in.
	/// </summary>
	public void Release()
	{
		Body?.DataContext = null;
		(TitleHost.Content as Control)?.DataContext = null;
	}

	/// <summary>Where on the frame the header was grabbed, so a floating window lands under the pointer.</summary>
	public Point GrabOffset => Header.TranslatePoint(_drag.GrabOffset, this) ?? _drag.GrabOffset;

	// Screen positions (device pixels): the drag can cross windows, e.g. from a floating one
	public event Action<WidgetFrame, PixelPoint>? DragStarted;
	public event Action<WidgetFrame, PixelPoint>? DragMoved;
	public event Action<WidgetFrame, PixelPoint>? DragDropped;
	public event Action<WidgetFrame>? DragCancelled;
}
