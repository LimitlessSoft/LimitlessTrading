using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LT.App.Controls;
using LT.App.Helpers;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.Settings;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;
using LT.Contracts.Interfaces.IManagers;

namespace LT.App.Widgets.Settings;

/// <summary>
/// Ticker settings: a list of contracts, each with its own chart live update mode, added with the
/// app's ticker box (typed symbol, a picked IB listing or a recent contract, so an option charted
/// before can be picked too). Entries are keyed by conId: an option is its own entry, apart from
/// its underlying. Changes apply on Save, like every other setting.
/// </summary>
public partial class TickerSettingsSettingViewModel : SettingItemViewModel
{
	private readonly IContractManager _contractManager;

	public TickerSettingsSettingViewModel(
		IContractManager contractManager,
		IEnumerable<TickerSettingsDto> saved,
		string title,
		string? description = null,
		string? keywords = null
	)
		: base(title, description, keywords)
	{
		_contractManager = contractManager;
		foreach (var entry in saved)
			Entries.Add(CreateItem(entry.Contract, entry.ChartLiveUpdateMode));
		Entries.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasEntries));
	}

	/// <summary>The mode a newly added ticker starts with: what the list is mostly for.</summary>
	public static ChartLiveUpdateMode DefaultMode => ChartLiveUpdateMode.TickByTick;

	public ObservableCollection<TickerSettingItemViewModel> Entries { get; } = [];

	public bool HasEntries => Entries.Count > 0;

	/// <summary>The last add's problem (unknown ticker, already listed); null after a good one.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(HasStatus))]
	public partial string? Status { get; set; }

	public bool HasStatus => !string.IsNullOrEmpty(Status);

	/// <summary>Adds a resolved contract (a picked listing); one already listed is only reported.</summary>
	public void Add(ContractDto contract)
	{
		if (contract.ConId <= 0)
		{
			Status = $"{ContractHelpers.GetDisplayName(contract)} has no IB contract id.";
			return;
		}
		if (Entries.Any(x => x.Contract.ConId == contract.ConId))
		{
			Status = $"{ContractHelpers.GetDisplayName(contract)} is already in the list.";
			return;
		}
		_contractManager.AddRecentContract(contract);
		Entries.Add(CreateItem(contract, DefaultMode));
		Status = null;
	}

	/// <summary>Enter on a typed ticker: its first listing, as the chart's ticker box opens it.</summary>
	public async Task AddTickerAsync(string? text)
	{
		var ticker = text?.Trim().ToUpperInvariant() ?? string.Empty;
		if (ticker.Length == 0)
			return;
		try
		{
			Add(await _contractManager.ResolveTickerAsync(ticker));
		}
		catch (Exception ex)
		{
			Status = ErrorMessageHelpers.ToUserMessage(ex);
		}
	}

	public void Remove(TickerSettingItemViewModel item)
	{
		Entries.Remove(item);
		Status = null;
	}

	/// <summary>The recent contracts (empty box), or IB's listings of the typed symbol.</summary>
	public Task<IEnumerable<object>> GetSuggestionsAsync(
		string? text,
		CancellationToken cancellationToken
	) =>
		string.IsNullOrWhiteSpace(text)
			? Task.FromResult<IEnumerable<object>>(GetRecentSuggestions())
			: ContractSuggestionItem.SearchAsync(_contractManager, text, cancellationToken);

	public IReadOnlyList<ContractSuggestionItem> GetRecentSuggestions() =>
		_contractManager.GetRecentContracts().Select(x => new ContractSuggestionItem(x)).ToList();

	/// <summary>What Save writes, in list order.</summary>
	public List<TickerSettingsDto> ToDtos() =>
		[
			.. Entries.Select(x => new TickerSettingsDto
			{
				Contract = x.Contract,
				ChartLiveUpdateMode = x.Mode?.Mode ?? DefaultMode,
			}),
		];

	private TickerSettingItemViewModel CreateItem(ContractDto contract, ChartLiveUpdateMode mode) =>
		new(contract, ChartLiveUpdateOption.Find(mode), Remove);
}

/// <summary>One ticker of <see cref="TickerSettingsSettingViewModel"/>.</summary>
public partial class TickerSettingItemViewModel(
	ContractDto contract,
	ChartLiveUpdateOption mode,
	Action<TickerSettingItemViewModel> remove
) : ObservableObject
{
	public ContractDto Contract { get; } = contract;

	public string Name { get; } = ContractHelpers.GetDisplayName(contract);

	/// <summary>Tells listings apart: security type, exchange, currency.</summary>
	public string Details { get; } =
		string.Join(
			" · ",
			new[]
			{
				contract.SecurityType,
				ContractHelpers.GetListingExchange(contract),
				contract.Currency,
			}.Where(x => !string.IsNullOrEmpty(x))
		);

	public IReadOnlyList<ChartLiveUpdateOption> Modes => ChartLiveUpdateOption.All;

	[ObservableProperty]
	public partial ChartLiveUpdateOption? Mode { get; set; } = mode;

	[RelayCommand]
	private void Remove() => remove(this);
}
