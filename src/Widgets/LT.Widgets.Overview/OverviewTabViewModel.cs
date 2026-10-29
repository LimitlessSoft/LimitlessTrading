using LT.App.ViewModels;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.MarketData;
using LT.Contracts.Enums;

namespace LT.App.Widgets.Overview;

/// <summary>
/// One tab of the Overview widget. The widget owns the contract, its IB details and the quote
/// line; a tab only shows its part of them and loads anything else it needs itself. Everything
/// is called on the UI thread. A new tab: an <see cref="OverviewTab"/> value (at the end), a
/// subclass created in <see cref="OverviewViewModel"/>'s constructor and a view for it in
/// <c>OverviewView.axaml</c>'s data templates.
/// </summary>
public abstract class OverviewTabViewModel : ViewModelBase, IDisposable
{
	public abstract OverviewTab Kind { get; }

	/// <summary>The tab's header text.</summary>
	public abstract string Header { get; }

	/// <summary>
	/// The widget's contract (null: none), and its IB details once they are known (null while
	/// loading, disconnected or not found). Called again whenever either changes;
	/// <paramref name="load"/> is cancelled at the next call, so work started for the previous
	/// contract can't write into this one.
	/// </summary>
	public abstract void Show(
		ContractDto? contract,
		ContractDetailsDto? details,
		CancellationToken load
	);

	/// <summary>The widget's latest quote (throttled to the UI flush); null when there is none.</summary>
	public virtual void OnQuote(QuoteDto? quote) { }

	public virtual void Dispose() => GC.SuppressFinalize(this);
}
