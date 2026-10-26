using LT.App.ViewModels;
using LT.Contracts.Dtos.Contracts;
using LT.Domain.Managers;
using LT.Tests.Fakes;

namespace LT.Tests.WidgetsCore;

/// <summary>The click-to-edit ticker title: what commit, cancel and a pick hand to the widget.</summary>
public class TickerTitleEditorTests
{
	private readonly TestCache _cache = new();
	private readonly List<string> _opened = [];
	private readonly List<ContractDto> _picked = [];
	private readonly TickerTitleEditor _editor;

	public TickerTitleEditorTests() =>
		_editor = new TickerTitleEditor(
			new ContractManager(new FakeIbkrApiClient(), _cache.Settings, _cache.Manager),
			() => true,
			ticker =>
			{
				_opened.Add(ticker);
				return Task.CompletedTask;
			},
			_picked.Add
		);

	[Fact]
	public async Task Commit_opens_the_trimmed_upper_cased_ticker_and_closes_the_box()
	{
		_editor.Begin();

		await _editor.CommitAsync("  msft ");

		Assert.Equal(["MSFT"], _opened);
		Assert.False(_editor.IsEditing);
	}

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	[InlineData(null)]
	public async Task Empty_commit_opens_nothing_and_closes_the_box(string? text)
	{
		_editor.Begin();

		await _editor.CommitAsync(text);

		Assert.Empty(_opened);
		Assert.False(_editor.IsEditing);
	}

	[Fact]
	public void Cancel_opens_nothing_and_restores_the_title()
	{
		_editor.Begin();
		Assert.True(_editor.IsEditing);

		_editor.Cancel();

		Assert.Empty(_opened);
		Assert.Empty(_picked);
		Assert.False(_editor.IsEditing);
	}

	[Fact]
	public void A_picked_listing_is_opened_as_is()
	{
		var spy = new ContractDto
		{
			ConId = 756733,
			Symbol = "SPY",
			SecurityType = "STK",
		};
		_editor.Begin();

		_editor.Pick(spy);

		Assert.Same(spy, Assert.Single(_picked));
		Assert.Empty(_opened);
		Assert.False(_editor.IsEditing);
	}
}
