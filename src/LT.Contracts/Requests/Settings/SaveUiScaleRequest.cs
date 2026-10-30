namespace LT.Contracts.Requests.Settings;

public class SaveUiScaleRequest
{
	/// <summary>Percent of the normal size (<see cref="Constants.UiScaleConstants"/>).</summary>
	public int Percent { get; set; }
}
