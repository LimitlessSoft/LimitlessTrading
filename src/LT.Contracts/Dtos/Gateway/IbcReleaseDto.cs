namespace LT.Contracts.Dtos.Gateway;

public class IbcReleaseDto
{
	public string Version { get; set; } = string.Empty;
	public IReadOnlyList<IbcReleaseAssetDto> Assets { get; set; } = [];
}

public class IbcReleaseAssetDto
{
	public string Name { get; set; } = string.Empty;
	public string DownloadUrl { get; set; } = string.Empty;
	public long Size { get; set; }
}
