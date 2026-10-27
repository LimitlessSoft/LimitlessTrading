using LSCore.Exceptions;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Requests.Watchlists;
using LT.Domain.Managers;
using LT.Tests.Fakes;

namespace LT.Tests.Domain.Managers;

/// <summary>Custom watchlist columns: adding, renaming, deleting, and the values typed into them.</summary>
public class WatchlistManagerColumnsTests
{
	private readonly InMemorySettingsRepository _settings = new();
	private readonly WatchlistManager _manager;
	private readonly string _id;

	public WatchlistManagerColumnsTests()
	{
		LSCoreContainer.EnsureInitialized();
		_manager = new WatchlistManager(_settings, new FakeContractManager());
		_id = _manager.GetActive().Id;
		_manager.Add(_id, Stock(1, "AAPL"));
		_manager.Add(_id, Stock(2, "MSFT"));
	}

	[Fact]
	public void A_column_is_added_renamed_and_kept_in_order()
	{
		var target = AddColumn("Target");
		AddColumn("Note");

		_manager.SaveColumn(
			new SaveWatchlistColumnRequest
			{
				WatchlistId = _id,
				ColumnId = target,
				Name = " Price target ",
			}
		);

		Assert.Equal(["Price target", "Note"], _manager.GetColumns(_id).Select(x => x.Name));
	}

	[Fact]
	public void New_column_names_count_up_from_one()
	{
		Assert.Equal("Column 1", _manager.GetNewColumnName(_id));
		AddColumn("Column 1");
		Assert.Equal("Column 2", _manager.GetNewColumnName(_id));
	}

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	public void A_blank_column_name_is_refused(string name)
	{
		var ex = Assert.ThrowsAny<Exception>(() => AddColumn(name));
		Assert.Contains("Column name is required", ex.Message);
		Assert.Empty(_manager.GetColumns(_id));
	}

	[Fact]
	public void A_taken_column_name_is_refused_ignoring_case()
	{
		AddColumn("Target");

		var ex = Assert.Throws<LSCoreBadRequestException>(() => AddColumn("target"));

		Assert.Contains("already exists", ex.Message);
	}

	[Fact]
	public void At_most_the_column_limit_can_be_added()
	{
		for (var i = 0; i < WatchlistConstants.MaxCustomColumns; i++)
			AddColumn($"C{i}");

		Assert.Throws<LSCoreBadRequestException>(() => AddColumn("One too many"));
		Assert.Equal(WatchlistConstants.MaxCustomColumns, _manager.GetColumns(_id).Count);
	}

	[Fact]
	public void Values_are_set_trimmed_changed_and_cleared()
	{
		var column = AddColumn("Target");
		var changes = 0;
		_manager.Changed += () => changes++;

		SetCell(column, Stock(1, "AAPL"), " 250 ");
		Assert.Equal("250", Value(column, 1));

		SetCell(column, Stock(1, "AAPL"), "buy");
		Assert.Equal("buy", Value(column, 1));

		SetCell(column, Stock(1, "AAPL"), "buy");
		SetCell(column, Stock(1, "AAPL"), "  ");
		Assert.Empty(_manager.GetColumns(_id)[0].Cells);
		Assert.Equal(3, changes);
	}

	[Fact]
	public void A_value_longer_than_the_limit_is_refused()
	{
		var column = AddColumn("Note");

		var ex = Assert.ThrowsAny<Exception>(() =>
			SetCell(
				column,
				Stock(1, "AAPL"),
				new string('x', WatchlistConstants.CellValueMaxLength + 1)
			)
		);

		Assert.Contains($"at most {WatchlistConstants.CellValueMaxLength}", ex.Message);
		Assert.Empty(_manager.GetColumns(_id)[0].Cells);
	}

	[Fact]
	public void A_value_needs_the_item_in_the_watchlist()
	{
		var column = AddColumn("Note");

		Assert.Throws<LSCoreBadRequestException>(() => SetCell(column, Stock(9, "TSLA"), "x"));
	}

	[Fact]
	public void Removing_an_item_drops_its_values()
	{
		var column = AddColumn("Note");
		SetCell(column, Stock(1, "AAPL"), "a");
		SetCell(column, Stock(2, "MSFT"), "m");

		_manager.Remove(_id, Stock(1, "AAPL"));

		var cell = Assert.Single(_manager.GetColumns(_id)[0].Cells);
		Assert.Equal((2, "m"), (cell.ConId, cell.Value));
	}

	[Fact]
	public void Deleting_a_column_drops_it_and_its_values()
	{
		var column = AddColumn("Note");
		SetCell(column, Stock(1, "AAPL"), "a");

		_manager.DeleteColumn(_id, column);

		Assert.Empty(_manager.GetColumns(_id));
	}

	private string AddColumn(string name) =>
		_manager.SaveColumn(new SaveWatchlistColumnRequest { WatchlistId = _id, Name = name }).Id;

	private void SetCell(string column, ContractDto contract, string value) =>
		_manager.SetCell(
			new SetWatchlistCellRequest
			{
				WatchlistId = _id,
				ColumnId = column,
				Contract = contract,
				Value = value,
			}
		);

	private string Value(string column, int conId) =>
		_manager
			.GetColumns(_id)
			.Single(x => x.Id == column)
			.Cells.Single(x => x.ConId == conId)
			.Value;

	private static ContractDto Stock(int conId, string symbol) =>
		new()
		{
			ConId = conId,
			Symbol = symbol,
			SecurityType = "STK",
			Exchange = "SMART",
			Currency = "USD",
		};
}
