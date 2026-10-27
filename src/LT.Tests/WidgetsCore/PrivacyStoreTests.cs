using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Data;
using LT.App.Controls;
using LT.App.Helpers;
using LT.App.Stores;
using LT.Domain.Managers;
using LT.Tests.Fakes;

namespace LT.Tests.WidgetsCore;

/// <summary>
/// Privacy mode: the store and the view side (<see cref="Sensitive"/>) together, in one class
/// because <see cref="Sensitive.IsHidden"/> is app-wide and tests in one class don't run in
/// parallel.
/// </summary>
public class PrivacyStoreTests : IDisposable
{
	private readonly InMemorySettingsRepository _repository = new();
	private readonly SettingsManager _settings;

	public PrivacyStoreTests() =>
		_settings = new SettingsManager(_repository, new FakeIbkrApiClient());

	public void Dispose() => TestUiThread.Invoke(() => Sensitive.IsHidden = false);

	[Fact]
	public void Values_show_until_privacy_mode_is_switched_on()
	{
		var store = TestUiThread.Invoke(() => new PrivacyStore(_settings));

		Assert.False(store.IsSensitiveHidden);
		Assert.False(Sensitive.IsHidden);
		Assert.Equal("1,234.56", Sensitive.Apply("1,234.56"));
	}

	[Fact]
	public void Switching_it_on_masks_values_and_is_saved_for_the_next_start()
	{
		var store = TestUiThread.Invoke(() => new PrivacyStore(_settings));

		TestUiThread.Invoke(() => store.IsSensitiveHidden = true);

		Assert.True(Sensitive.IsHidden);
		Assert.Equal(Sensitive.Mask, Sensitive.Apply("1,234.56"));
		Assert.True(_repository.Settings.HideSensitiveValues);
		var next = TestUiThread.Invoke(() => new PrivacyStore(_settings));
		Assert.True(next.IsSensitiveHidden);

		TestUiThread.Invoke(() => next.IsSensitiveHidden = false);

		Assert.False(Sensitive.IsHidden);
		Assert.False(_repository.Settings.HideSensitiveValues);
	}

	[Fact]
	public void An_empty_value_is_not_masked()
	{
		TestUiThread.Invoke(() => Sensitive.IsHidden = true);

		Assert.Equal(string.Empty, Sensitive.Apply(string.Empty));
		Assert.Null(Sensitive.Apply(null));
	}

	[Fact]
	public void A_sensitive_text_masks_live_without_touching_its_bound_value()
	{
		TestUiThread.Invoke(() =>
		{
			var row = new Row { Value = "U1234567" };
			var text = new SensitiveTextBlock { DataContext = row };
			// TwoWay like a DataGrid column's binding: the mask must never reach the row
			text.Bind(
				TextBlock.TextProperty,
				new ReflectionBinding(nameof(Row.Value)) { Mode = BindingMode.TwoWay }
			);
			Assert.Equal("U1234567", text.ShownText);

			Sensitive.IsHidden = true;
			Assert.Equal(Sensitive.Mask, text.ShownText);
			Assert.Equal("U1234567", text.Text);

			row.Value = "U7654321";
			Assert.Equal(Sensitive.Mask, text.ShownText);

			Sensitive.IsHidden = false;
			Assert.Equal("U7654321", text.ShownText);
			Assert.Equal("U7654321", row.Value);
		});
	}

	private sealed class Row : INotifyPropertyChanged
	{
		public event PropertyChangedEventHandler? PropertyChanged;

		public string Value
		{
			get;
			set
			{
				field = value;
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
			}
		} = string.Empty;
	}
}
