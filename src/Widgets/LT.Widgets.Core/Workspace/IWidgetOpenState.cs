namespace LT.App.Workspace;

/// <summary>
/// A widget view model that holds scarce IB resources only while its widget is open (e.g. the
/// Depth widget's depth subscription, one of IB's three): told to let go as soon as the widget is
/// closed, before its view model is disposed.
/// </summary>
public interface IWidgetOpenState
{
	/// <summary>Called with false when the widget is closed, true when it is placed again.</summary>
	void SetOpen(bool isOpen);
}
