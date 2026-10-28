using LT.App.Stores;
using LT.App.Workspace;
using LT.Contracts.Dtos.Settings;
using LT.Contracts.Enums;
using LT.Contracts.Requests.Settings;
using LT.Domain.Managers;
using LT.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;

namespace LT.Tests.Workspace;

/// <summary>
/// Menu → Import layout applies the imported layout live: every workspace is replaced by the
/// file's, with each widget's settings, and the replaced widgets are disposed.
/// </summary>
public class WorkspaceLayoutImportTests
{
	private sealed class FakeWidget : IWidgetState, IDisposable
	{
		public string? Setting { get; set; }
		public bool IsDisposed { get; private set; }

		public void LoadState(WidgetPlacementDto placement) => Setting = placement.WatchlistId;

		public void SaveState(WidgetPlacementDto placement) => placement.WatchlistId = Setting;

		public void Dispose() => IsDisposed = true;
	}

	private readonly SettingsManager _settings;
	private readonly WorkspaceWindows _windows;

	public WorkspaceLayoutImportTests()
	{
		LSCoreContainer.EnsureInitialized();
		_settings = new SettingsManager(new InMemorySettingsRepository(), new FakeIbkrApiClient());
		// An empty main window: no first-run widgets
		_settings.SaveLayout(new LayoutDto());
		var services = new ServiceCollection();
		foreach (var definition in WidgetCatalog.Definitions)
			services.AddTransient(definition.ViewModelType, _ => new FakeWidget());
		_windows = new WorkspaceWindows(
			new WidgetViewModelFactory(services.BuildServiceProvider()),
			_settings,
			new AppearanceStore(_settings)
		);
	}

	// What another install exported: a watchlist on the left of the main window, a second window
	// with orders in its center
	private static string ExportedElsewhere()
	{
		var other = new SettingsManager(new InMemorySettingsRepository(), new FakeIbkrApiClient());
		other.SaveLayout(
			new LayoutDto
			{
				WindowWidth = 1500,
				WindowHeight = 900,
				LeftWidgetId = "watchlist",
				Widgets =
				[
					new WidgetPlacementDto
					{
						Id = "watchlist",
						Tool = WorkspaceTool.Watchlist,
						Dock = WidgetDock.Left,
						WatchlistId = "tech",
					},
				],
				Windows =
				[
					new WorkspaceWindowDto
					{
						Id = "w2",
						Name = "Orders",
						Width = 900,
						Height = 700,
						Widgets =
						[
							new WidgetPlacementDto
							{
								Id = "orders",
								Tool = WorkspaceTool.Orders,
								Dock = WidgetDock.Center,
							},
						],
					},
				],
			}
		);
		return other.ExportLayout();
	}

	private void Import(string json) =>
		_windows.Replace(_settings.ImportLayout(new ImportLayoutRequest { Json = json }));

	[Fact]
	public void Import_replaces_every_window_with_the_files_and_disposes_the_old_widgets()
	{
		var oldMain = _windows.Main.Spawn(WorkspaceTool.Account);
		var oldWindow = _windows.Open();
		var oldExtra = oldWindow.Workspace.Spawn(WorkspaceTool.News);
		var mainChanged = false;
		_windows.MainChanged += () => mainChanged = true;

		Import(ExportedElsewhere());

		Assert.True(mainChanged);
		Assert.True(((FakeWidget)oldMain.Content).IsDisposed);
		Assert.True(((FakeWidget)oldExtra.Content).IsDisposed);
		Assert.DoesNotContain(oldWindow, _windows.Windows);
		var watchlist = Assert.Single(_windows.Main.Widgets);
		Assert.Equal((WorkspaceTool.Watchlist, WidgetDock.Left), (watchlist.Tool, watchlist.Dock));
		Assert.Equal("tech", ((FakeWidget)watchlist.Content).Setting);
		var window = Assert.Single(_windows.Windows);
		Assert.Equal("Orders", window.Name);
		Assert.Equal(WorkspaceTool.Orders, Assert.Single(window.Workspace.Widgets).Tool);
	}

	[Fact]
	public void A_refused_file_leaves_every_window_as_it_was()
	{
		var account = _windows.Main.Spawn(WorkspaceTool.Account);
		var window = _windows.Open();
		var main = _windows.Main;

		Assert.ThrowsAny<Exception>(() => Import("{ \"format\": \"LimitlessTrading.Layout\""));

		Assert.Same(main, _windows.Main);
		Assert.Equal([account], _windows.Main.Widgets);
		Assert.False(((FakeWidget)account.Content).IsDisposed);
		Assert.Equal([window], _windows.Windows);
	}
}
