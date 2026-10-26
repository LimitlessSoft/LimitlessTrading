using CommunityToolkit.Mvvm.ComponentModel;
using LT.App.Controls;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Interfaces.IManagers;

namespace LT.App.ViewModels;

/// <summary>
/// A widget header title that turns into a ticker box on click (chart, option chain), shown by
/// <see cref="TickerTitle"/>. Typed text is trimmed and upper-cased; an empty commit only closes
/// the box. The ticker or picked listing goes to the widget's own open path, so it loads exactly
/// as a contract arriving any other way would.
/// </summary>
/// <param name="openTicker">Resolves a typed ticker and shows it.</param>
/// <param name="openContract">Shows a listing picked from the suggestions (already resolved).</param>
public partial class TickerTitleEditor(
	IContractManager contractManager,
	Func<bool> isConnected,
	Func<string, Task> openTicker,
	Action<ContractDto> openContract
) : ObservableObject
{
	/// <summary>True while the title is a ticker box.</summary>
	[ObservableProperty]
	public partial bool IsEditing { get; private set; }

	public void Begin() => IsEditing = true;

	/// <summary>Esc (or clicking away, where that cancels): back to the title, nothing opened.</summary>
	public void Cancel() => IsEditing = false;

	/// <summary>Enter on typed text: opens the upper-cased ticker; blank text just closes.</summary>
	public Task CommitAsync(string? text)
	{
		IsEditing = false;
		var ticker = text?.Trim().ToUpperInvariant() ?? string.Empty;
		return ticker.Length == 0 ? Task.CompletedTask : openTicker(ticker);
	}

	/// <summary>A listing picked from the suggestions.</summary>
	public void Pick(ContractDto contract)
	{
		IsEditing = false;
		openContract(contract);
	}

	/// <summary>Suggestions for an empty box: the recent contracts.</summary>
	public IReadOnlyList<ContractSuggestionItem> GetRecentSuggestions() =>
		contractManager.GetRecentContracts().Select(x => new ContractSuggestionItem(x)).ToList();

	/// <summary>Suggestions for typed text: IB's listings of the symbol.</summary>
	public Task<IEnumerable<object>> GetSuggestionsAsync(
		string? text,
		CancellationToken cancellationToken
	) =>
		string.IsNullOrWhiteSpace(text)
			? Task.FromResult<IEnumerable<object>>(GetRecentSuggestions())
		: isConnected()
			? ContractSuggestionItem.SearchAsync(contractManager, text, cancellationToken)
		: Task.FromResult(Enumerable.Empty<object>());
}
