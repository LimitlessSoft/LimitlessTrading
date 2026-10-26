namespace LT.App.Workspace;

/// <summary>
/// A widget view model that holds scarce IB resources only while its widget is open. A closed
/// single-instance widget keeps its view model, so it is told to let go (e.g. the Depth widget's
/// depth subscription, one of IB's three) and to start again when reopened.
/// </summary>
public interface IWidgetOpenState
{
	/// <summary>Called with false when the widget is closed, true when it is placed again.</summary>
	void SetOpen(bool isOpen);
}
