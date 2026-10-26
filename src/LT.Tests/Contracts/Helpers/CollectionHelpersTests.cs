using System.Collections.ObjectModel;
using System.Collections.Specialized;
using LT.Contracts.Helpers;

namespace LT.Tests.Contracts.Helpers;

public class CollectionHelpersTests
{
	[Fact]
	public void An_unchanged_order_touches_nothing()
	{
		var list = new ObservableCollection<string>(["A", "B", "C"]);
		var changes = 0;
		list.CollectionChanged += (_, _) => changes++;

		Assert.Equal(0, CollectionHelpers.ApplyOrder(list, ["A", "B", "C"]));
		Assert.Equal(0, changes);
	}

	[Fact]
	public void One_item_going_to_the_end_is_one_move()
	{
		var list = new List<string> { "A", "B", "C", "D" };

		Assert.Equal(1, CollectionHelpers.ApplyOrder(list, ["B", "C", "D", "A"]));
		Assert.Equal(["B", "C", "D", "A"], list);
	}

	[Fact]
	public void A_reversal_keeps_one_and_moves_the_rest()
	{
		var list = new List<string> { "A", "B", "C", "D", "E" };

		Assert.Equal(4, CollectionHelpers.ApplyOrder(list, ["E", "D", "C", "B", "A"]));
		Assert.Equal(["E", "D", "C", "B", "A"], list);
	}

	[Fact]
	public void Moves_are_raised_as_removes_and_adds_only()
	{
		// Avalonia's DataGrid ignores Move notifications
		var list = new ObservableCollection<string>(["A", "B", "C", "D", "E", "F"]);
		var actions = new List<NotifyCollectionChangedAction>();
		list.CollectionChanged += (_, e) => actions.Add(e.Action);
		string[] order = ["F", "B", "A", "D", "C", "E"];

		CollectionHelpers.ApplyOrder(list, order);

		Assert.Equal(order, list);
		Assert.All(
			actions,
			x =>
				Assert.True(
					x is NotifyCollectionChangedAction.Remove or NotifyCollectionChangedAction.Add
				)
		);
	}

	[Fact]
	public void Random_orders_always_come_out_right()
	{
		var random = new Random(42);
		for (var run = 0; run < 200; run++)
		{
			var items = Enumerable.Range(0, random.Next(0, 30)).Select(x => $"r{x}").ToList();
			var list = items.OrderBy(_ => random.Next()).ToList();
			var order = items.OrderBy(_ => random.Next()).ToList();

			CollectionHelpers.ApplyOrder(list, order);

			Assert.Equal(order, list);
		}
	}

	[Fact]
	public void An_order_with_other_items_is_rejected() =>
		Assert.Throws<ArgumentException>(() =>
			CollectionHelpers.ApplyOrder(new List<string> { "A", "B" }, ["A", "C"])
		);
}
