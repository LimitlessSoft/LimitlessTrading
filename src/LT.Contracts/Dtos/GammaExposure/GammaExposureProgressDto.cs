namespace LT.Contracts.Dtos.GammaExposure;

/// <summary>A reading under way: options done (answered or timed out) of those asked for.</summary>
public record GammaExposureProgressDto(int Done, int Total);
