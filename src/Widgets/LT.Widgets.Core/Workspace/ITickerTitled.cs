using System.ComponentModel;
using LT.App.ViewModels;

namespace LT.App.Workspace;

/// <summary>
/// A widget whose header title is its ticker and can be clicked to type another
/// (<see cref="LT.App.Controls.TickerTitle"/>).
/// </summary>
public interface ITickerTitled : INotifyPropertyChanged
{
	string Title { get; }

	TickerTitleEditor TickerEditor { get; }
}
