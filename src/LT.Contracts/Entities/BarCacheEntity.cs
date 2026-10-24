namespace LT.Contracts.Entities;

public class BarCacheEntity
{
	public string Key { get; set; } = string.Empty;
	public DateTimeOffset FetchedAt { get; set; }
	public List<BarEntity> Bars { get; set; } = [];
}
