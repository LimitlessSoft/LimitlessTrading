using LT.App.Helpers;
using LT.App.Stores;
using LT.Contracts.Entities;
using LT.Contracts.Interfaces.IRepositories;
using LT.Domain.Managers;
using LT.Tests.Fakes;

namespace LT.Tests.WidgetsCore;

/// <summary>
/// The UI scale store and its view side (<see cref="UiScale"/>) together, in one class because
/// <see cref="UiScale.Factor"/> is app-wide and tests in one class don't run in parallel.
/// </summary>
public class UiScaleStoreTests : IDisposable
{
	private readonly InMemorySettingsRepository _repository = new();
	private readonly SettingsManager _settings;

	public UiScaleStoreTests()
	{
		LSCoreContainer.EnsureInitialized();
		_settings = new SettingsManager(_repository, new FakeIbkrApiClient());
	}

	public void Dispose() => TestUiThread.Invoke(() => UiScale.Factor = 1);

	[Fact]
	public void It_starts_at_the_saved_scale_and_applies_it()
	{
		_repository.Settings.UiScalePercent = 125;

		var store = TestUiThread.Invoke(() => new UiScaleStore(_settings));

		Assert.Equal(125, store.Percent);
		Assert.Equal(new UiScaleOption(125), store.Selected);
		Assert.Equal(1.25, UiScale.Factor);
	}

	[Fact]
	public void Picking_a_scale_applies_it_at_once_and_is_saved_for_the_next_start()
	{
		var store = TestUiThread.Invoke(() => new UiScaleStore(_settings));
		var changes = 0;
		Action counted = () => changes++;
		UiScale.Changed += counted;
		try
		{
			TestUiThread.Invoke(() => store.Selected = new UiScaleOption(150));
		}
		finally
		{
			UiScale.Changed -= counted;
		}

		Assert.Equal(1, changes);
		Assert.Equal(1.5, UiScale.Factor);
		Assert.Equal(150, _repository.Settings.UiScalePercent);
		var next = TestUiThread.Invoke(() => new UiScaleStore(_settings));
		Assert.Equal(150, next.Percent);
	}

	[Fact]
	public void A_hand_edited_value_shows_in_the_pickers()
	{
		_repository.Settings.UiScalePercent = 115;

		var store = TestUiThread.Invoke(() => new UiScaleStore(_settings));

		Assert.Contains(new UiScaleOption(115), store.Options);
		Assert.Equal(new UiScaleOption(115), store.Selected);
	}

	[Fact]
	public void Opening_does_not_rewrite_the_settings()
	{
		var saves = new CountingSettingsRepository();
		var manager = new SettingsManager(saves, new FakeIbkrApiClient());

		TestUiThread.Invoke(() => new UiScaleStore(manager));

		Assert.Equal(0, saves.Saves);
	}

	private sealed class CountingSettingsRepository : ISettingsRepository
	{
		private SettingsEntity _settings = new();

		public int Saves { get; private set; }

		public SettingsEntity Get() => _settings;

		public void Save(SettingsEntity settings)
		{
			Saves++;
			_settings = settings;
		}
	}
}
