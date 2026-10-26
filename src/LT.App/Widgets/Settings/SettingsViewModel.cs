using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using LT.App.Helpers;
using LT.App.ViewModels;
using LT.Contracts.Constants;
using LT.Contracts.Enums;
using LT.Contracts.Interfaces.IManagers;
using LT.Contracts.Requests.Settings;

namespace LT.App.Widgets.Settings;

/// <summary>
/// The settings window: a searchable tree of pages on the left, the selected page's tabs on the
/// right. Changes apply on Save.
/// </summary>
public partial class SettingsViewModel : ViewModelBase
{
	private readonly ISettingsManager _settingsManager;

	private readonly ChoiceSettingViewModel _marketDataType = new(
		"Market data type",
		[.. Enum.GetValues<MarketDataType>().Cast<object>()],
		"Delayed still shows live data for instruments you have a subscription for. Applies to new subscriptions.",
		"live frozen delayed quotes subscription"
	);

	private readonly ChoiceSettingViewModel _layoutStyle = new(
		"Layout style",
		[.. Enum.GetValues<LayoutStyle>().Cast<object>()],
		"Cards: widgets are rounded tiles with gaps between them. Flush: widgets fill the window edge to edge, split by thin lines.",
		"theme look appearance rounded corners gaps spacing compact edge"
	);

	private readonly BoolSettingViewModel _autoReconnect = new(
		"Reconnect automatically when the connection drops",
		keywords: "auto reconnect retry backoff"
	);

	private readonly NumberSettingViewModel _heartbeatSeconds = new(
		"Heartbeat interval (seconds)",
		ConnectionConstants.HeartbeatSecondsMin,
		ConnectionConstants.HeartbeatSecondsMax,
		"How often the connection is checked; a missed heartbeat counts as a dropped connection.",
		"ping keepalive health"
	);

	private readonly BoolSettingViewModel _stopGatewayOnExit = new(
		"Stop IB Gateway when the app closes",
		"Turn off to keep the Gateway logged in between runs; the next Log in reuses it without 2FA.",
		"ibc login 2fa exit quit"
	);

	private readonly BoolSettingViewModel _checkForUpdates = new(
		"Check for updates",
		"On start and every 6 hours; a newer version shows in the title bar. Installing always waits for you.",
		"version release upgrade"
	);

	public SettingsViewModel(ISettingsManager settingsManager)
	{
		_settingsManager = settingsManager;
		var settings = settingsManager.GetAppSettings();
		_marketDataType.Value = settings.MarketDataType;
		_autoReconnect.Value = settings.AutoReconnect;
		_heartbeatSeconds.Value = settings.HeartbeatSeconds;
		_stopGatewayOnExit.Value = settings.StopGatewayOnExit;
		_checkForUpdates.Value = settings.CheckForUpdates;
		_layoutStyle.Value = settings.LayoutStyle;

		Pages =
		[
			new SettingsPageViewModel(
				"General",
				children: [new SettingsPageViewModel("Updates", [new("Updates", _checkForUpdates)])]
			),
			new SettingsPageViewModel("Appearance", [new("Layout", _layoutStyle)]),
			new SettingsPageViewModel(
				"Connection",
				[
					new("Reconnect", _autoReconnect, _heartbeatSeconds),
					new("IB Gateway", _stopGatewayOnExit),
				]
			),
			new SettingsPageViewModel("Market data", [new("Quotes", _marketDataType)]),
		];
		ApplySearch();
	}

	/// <summary>Every page of the tree, as defined; the tree shows <see cref="VisiblePages"/>.</summary>
	public IReadOnlyList<SettingsPageViewModel> Pages { get; }

	/// <summary>Top-level pages left by the search.</summary>
	public ObservableCollection<SettingsPageViewModel> VisiblePages { get; } = [];

	[ObservableProperty]
	public partial string? SearchText { get; set; }

	/// <summary>The tree's selection: a page, or a group (which shows its first page).</summary>
	[ObservableProperty]
	public partial SettingsPageViewModel? SelectedNode { get; set; }

	/// <summary>The page shown on the right.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(HasPage))]
	public partial SettingsPageViewModel? SelectedPage { get; set; }

	public bool HasPage => SelectedPage != null;

	/// <summary>Nothing matches the search.</summary>
	public bool HasNoResults => VisiblePages.Count == 0;

	[ObservableProperty]
	public partial string? ErrorMessage { get; set; }

	partial void OnSearchTextChanged(string? value) => ApplySearch();

	partial void OnSelectedNodeChanged(SettingsPageViewModel? value)
	{
		if (value != null)
			SelectedPage = value.FirstPage;
	}

	/// <summary>Returns true when the settings were saved and the dialog can close.</summary>
	public bool Save()
	{
		try
		{
			_settingsManager.SaveAppSettings(
				new SaveAppSettingsRequest
				{
					MarketDataType = (MarketDataType)_marketDataType.Value!,
					AutoReconnect = _autoReconnect.Value,
					HeartbeatSeconds = (int)(_heartbeatSeconds.Value ?? 0),
					StopGatewayOnExit = _stopGatewayOnExit.Value,
					CheckForUpdates = _checkForUpdates.Value,
					LayoutStyle = (LayoutStyle)_layoutStyle.Value!,
				}
			);
			return true;
		}
		catch (Exception ex)
		{
			ErrorMessage = ErrorMessageHelpers.ToUserMessage(ex);
			return false;
		}
	}

	// Filters the tree; the shown page stays if it still matches, else the first match opens
	private void ApplySearch()
	{
		VisiblePages.Clear();
		foreach (var page in Pages)
			if (page.Filter(SearchText))
				VisiblePages.Add(page);
		OnPropertyChanged(nameof(HasNoResults));

		if (SelectedPage is { } shown && IsVisible(shown))
			return;
		var first = VisiblePages.Select(x => x.FirstPage).FirstOrDefault(x => x != null);
		SelectedNode = first;
		SelectedPage = first;
	}

	private bool IsVisible(SettingsPageViewModel page) =>
		page.Parent is { } parent
			? parent.VisibleChildren.Contains(page) && IsVisible(parent)
			: VisiblePages.Contains(page);
}
