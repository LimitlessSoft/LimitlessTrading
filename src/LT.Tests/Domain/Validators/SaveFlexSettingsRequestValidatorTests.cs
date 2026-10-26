using LSCore.Exceptions;
using LT.Contracts.Requests.Flex;
using LT.Domain.Managers;
using LT.Tests.Fakes;

namespace LT.Tests.Domain.Validators;

public class SaveFlexSettingsRequestValidatorTests
{
	private const string Token = "123456789012345678901234";
	private readonly InMemorySettingsRepository _repository = new();
	private readonly SettingsManager _manager;

	public SaveFlexSettingsRequestValidatorTests()
	{
		LSCoreContainer.EnsureInitialized();
		_manager = new SettingsManager(_repository, new FakeIbkrApiClient());
	}

	[Fact]
	public void All_empty_turns_flex_off() =>
		_manager.SaveFlexSettings(new SaveFlexSettingsRequest());

	[Fact]
	public void A_token_and_an_activity_query_are_saved_trimmed()
	{
		_manager.SaveFlexSettings(
			new SaveFlexSettingsRequest
			{
				Token = $" {Token} ",
				ActivityQueryId = "123456 ",
				TradeConfirmQueryId = "654321",
			}
		);

		var saved = _manager.GetFlexSettings();
		Assert.Equal(
			(Token, "123456", "654321"),
			(saved.Token, saved.ActivityQueryId, saved.TradeConfirmQueryId)
		);
		Assert.True(saved.IsConfigured);
	}

	[Theory]
	[InlineData("", "123", "")]
	[InlineData("", "", "123")]
	[InlineData(Token, "", "")]
	[InlineData("short", "123", "")]
	[InlineData("12345678901234567890-+", "123", "")]
	[InlineData(Token, "12a", "")]
	[InlineData(Token, "123", "1234567890123")]
	public void Incomplete_or_malformed_settings_are_refused(
		string token,
		string activity,
		string tradeConfirm
	)
	{
		Assert.ThrowsAny<LSCoreBadRequestException>(() =>
			_manager.SaveFlexSettings(
				new SaveFlexSettingsRequest
				{
					Token = token,
					ActivityQueryId = activity,
					TradeConfirmQueryId = tradeConfirm,
				}
			)
		);
		Assert.Equal(string.Empty, _repository.Settings.Flex.Token);
	}

	[Fact]
	public void Saving_the_same_values_raises_no_change()
	{
		var changes = 0;
		_manager.FlexSettingsChanged += _ => changes++;
		var request = new SaveFlexSettingsRequest { Token = Token, ActivityQueryId = "1" };

		_manager.SaveFlexSettings(request);
		_manager.SaveFlexSettings(request);

		Assert.Equal(1, changes);
	}
}
