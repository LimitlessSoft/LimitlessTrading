namespace LT.Contracts.Dtos.Calendar;

/// <summary>
/// A Wall Street Horizon event data request (reqWshEventData): WSH's JSON filter, the days and
/// the most events to return.
/// </summary>
public class WshEventQueryDto
{
	public string Filter { get; set; } = string.Empty;
	public DateOnly StartDate { get; set; }
	public DateOnly EndDate { get; set; }
	public int TotalLimit { get; set; }
}
