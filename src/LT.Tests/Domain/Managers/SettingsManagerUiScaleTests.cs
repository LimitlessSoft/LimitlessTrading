using System.Text.Json;
using LSCore.Exceptions;
using LT.Contracts.Constants;
using LT.Contracts.Entities;
using LT.Contracts.Helpers;
using LT.Contracts.Requests.Settings;
using LT.Domain.Managers;
using LT.Tests.Fakes;

namespace LT.Tests.Domain.Managers;

/// <summary>The app-wide UI scale: saved, loaded back and kept in range.</summary>
public class SettingsManagerUiScaleTests
{
	private readonly InMemorySettingsRepository _settings = new();
	private readonly SettingsManager _manager;

	public SettingsManagerUiScaleTests()
	{
		LSCoreContainer.EnsureInitialized();
		_manager = new SettingsManager(_settings, new FakeIbkrApiClient());
	}

	[Fact]
	public void A_settings_file_saved_before_the_scale_loads_at_normal_size()
	{
		var old = JsonSerializer.Deserialize<SettingsEntity>(
			"""{ "HideSensitiveValues": true }"""
		)!;
		_settings.Save(old);

		Assert.Equal(UiScaleConstants.DefaultPercent, _manager.GetUiScalePercent());
	}

	[Fact]
	public void A_saved_scale_is_loaded_back()
	{
		_manager.SaveUiScale(new SaveUiScaleRequest { Percent = 150 });

		Assert.Equal(150, _settings.Settings.UiScalePercent);
		Assert.Equal(150, _manager.GetUiScalePercent());
	}

	[Theory]
	[InlineData(UiScaleConstants.MinPercent)]
	[InlineData(UiScaleConstants.MaxPercent)]
	public void The_range_limits_are_allowed(int percent)
	{
		_manager.SaveUiScale(new SaveUiScaleRequest { Percent = percent });

		Assert.Equal(percent, _manager.GetUiScalePercent());
	}

	[Theory]
	[InlineData(UiScaleConstants.MinPercent - 1)]
	[InlineData(UiScaleConstants.MaxPercent + 1)]
	[InlineData(0)]
	public void A_scale_out_of_range_is_refused_and_nothing_changes(int percent)
	{
		_manager.SaveUiScale(new SaveUiScaleRequest { Percent = 125 });

		var error = Assert.Throws<LSCoreBadRequestException>(() =>
			_manager.SaveUiScale(new SaveUiScaleRequest { Percent = percent })
		);

		Assert.Contains("UI scale must be between 75 and 200", error.Message);
		Assert.Equal(125, _manager.GetUiScalePercent());
	}

	[Theory]
	[InlineData(10, UiScaleConstants.MinPercent)]
	[InlineData(-50, UiScaleConstants.MinPercent)]
	[InlineData(500, UiScaleConstants.MaxPercent)]
	[InlineData(115, 115)]
	public void A_hand_edited_value_out_of_range_loads_clamped(int saved, int loaded)
	{
		_settings.Settings.UiScalePercent = saved;

		Assert.Equal(loaded, _manager.GetUiScalePercent());
	}

	[Fact]
	public void The_pickers_offer_the_steps()
	{
		Assert.Equal(UiScaleConstants.StepPercents, UiScaleHelpers.Options(100));
	}

	[Fact]
	public void A_saved_value_between_steps_is_offered_in_order()
	{
		Assert.Equal([75, 90, 100, 110, 115, 125, 150, 175, 200], UiScaleHelpers.Options(115));
	}
}
