namespace LT.App.Workspace;

/// <summary>Widget icons as path data on a 24×24 grid (even-odd fill, so inner shapes cut out).</summary>
public static class WidgetIcons
{
	public const string Watchlist =
		"M1,12C3.5,6.5 7.5,4 12,4C16.5,4 20.5,6.5 23,12C20.5,17.5 16.5,20 12,20C7.5,20 3.5,17.5 1,12Z "
		+ "M3.3,12C5.5,7.8 8.6,6 12,6C15.4,6 18.5,7.8 20.7,12C18.5,16.2 15.4,18 12,18C8.6,18 5.5,16.2 3.3,12Z "
		+ "M12,8.5A3.5,3.5 0 1,1 12,15.5A3.5,3.5 0 1,1 12,8.5Z";

	public const string Positions =
		"M12,1L22,6L12,11L2,6Z M2,10L12,15L22,10V12L12,17L2,12Z M2,15L12,20L22,15V17L12,22L2,17Z";

	public const string Orders =
		"M5,2H15L20,7V22H5Z M8,10V12H17V10Z M8,14V16H17V14Z M8,18V20H14V18Z";

	public const string Executions = "M13,2L4,14H11L10,22L20,9H13Z";

	public const string Account =
		"M12,3A4.5,4.5 0 1,1 12,12A4.5,4.5 0 1,1 12,3Z M3,21C3,16.5 7,14 12,14C17,14 21,16.5 21,21Z";

	public const string Chart =
		"M3,3H5V19H21V21H3Z M7,9H9V17H7Z M11,5H13V17H11Z M15,11H17V17H15Z M19,7H21V17H19Z";

	public const string OptionChain =
		"M3,4H21V8H3Z M3,10H11V14H3Z M13,10H21V14H13Z M3,16H11V20H3Z M13,16H21V20H13Z";

	public const string Messages = "M3,3H21V17H8L3,21Z M7,7V9H17V7Z M7,11V13H14V11Z";
}
