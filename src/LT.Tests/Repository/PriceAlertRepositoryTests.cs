using LT.Contracts.Entities;
using LT.Contracts.Enums;
using LT.Repository.Repositories;

namespace LT.Tests.Repository;

/// <summary>The real price-alerts.json, in a temp folder deleted after each test.</summary>
public sealed class PriceAlertRepositoryTests : IDisposable
{
	private readonly string _directory = Path.Combine(
		Path.GetTempPath(),
		$"lt-price-alerts-tests-{Guid.NewGuid():N}"
	);

	private string FilePath => Path.Combine(_directory, "price-alerts.json");

	public void Dispose()
	{
		if (Directory.Exists(_directory))
			Directory.Delete(_directory, recursive: true);
	}

	[Fact]
	public void Missing_file_has_no_alerts() =>
		Assert.Empty(new PriceAlertRepository(FilePath).Get());

	[Fact]
	public void Unreadable_file_has_no_alerts()
	{
		Directory.CreateDirectory(_directory);
		File.WriteAllText(FilePath, "{ not json");

		Assert.Empty(new PriceAlertRepository(FilePath).Get());
	}

	[Fact]
	public void Round_trips_the_alerts()
	{
		var triggeredAt = new DateTimeOffset(2026, 10, 26, 15, 30, 0, TimeSpan.Zero);
		new PriceAlertRepository(FilePath).Save([
			new PriceAlertEntity
			{
				Id = "a",
				Contract = new ContractEntity { ConId = 265598, Symbol = "AAPL" },
				Price = 190.5,
				Condition = PriceAlertCondition.CrossingDown,
				Note = "support",
				CreatedAt = triggeredAt.AddHours(-1),
				TriggeredAt = triggeredAt,
				TriggerPrice = 190.4,
			},
		]);

		var alert = Assert.Single(new PriceAlertRepository(FilePath).Get());

		Assert.Equal("a", alert.Id);
		Assert.Equal(265598, alert.Contract.ConId);
		Assert.Equal(190.5, alert.Price);
		Assert.Equal(PriceAlertCondition.CrossingDown, alert.Condition);
		Assert.Equal("support", alert.Note);
		Assert.Equal(triggeredAt, alert.TriggeredAt);
		Assert.Equal(190.4, alert.TriggerPrice);
	}
}
