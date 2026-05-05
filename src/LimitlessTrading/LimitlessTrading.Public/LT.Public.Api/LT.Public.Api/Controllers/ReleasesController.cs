using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace LT.Public.Api.Controllers;

[ApiController]
[Route("api/releases")]
public class ReleasesController(IConfiguration config) : ControllerBase
{
    private record ReleaseMetadata(string Version, string FileName, string Checksum, DateTime PublishedAt);

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private string StoragePath => config["Releases:StoragePath"] ?? "/data/releases";

    [HttpGet]
    public IActionResult List()
    {
        if (!Directory.Exists(StoragePath))
            return Ok(Array.Empty<object>());

        var releases = Directory.GetDirectories(StoragePath)
            .Select(dir =>
            {
                var platform = Path.GetFileName(dir);
                var metaPath = Path.Combine(dir, "metadata.json");
                if (!System.IO.File.Exists(metaPath)) return null;

                var meta = JsonSerializer.Deserialize<ReleaseMetadata>(
                    System.IO.File.ReadAllText(metaPath), JsonOptions);
                if (meta is null) return null;

                var filePath = Path.Combine(dir, meta.FileName);
                var sizeMb = System.IO.File.Exists(filePath)
                    ? Math.Round((double)new FileInfo(filePath).Length / (1024 * 1024), 1)
                    : 0;

                return (object)new
                {
                    Platform = platform,
                    meta.Version,
                    Url = $"{Request.Scheme}://{Request.Host}/api/releases/download/{platform}",
                    meta.Checksum,
                    Size = $"{sizeMb} MB",
                    meta.PublishedAt
                };
            })
            .Where(r => r is not null)
            .ToList();

        return Ok(releases);
    }

    [HttpGet("download/{platform}")]
    public IActionResult Download(string platform)
    {
        var metaPath = Path.Combine(StoragePath, platform, "metadata.json");
        if (!System.IO.File.Exists(metaPath)) return NotFound();

        var meta = JsonSerializer.Deserialize<ReleaseMetadata>(
            System.IO.File.ReadAllText(metaPath), JsonOptions);
        if (meta is null) return NotFound();

        var filePath = Path.Combine(StoragePath, platform, meta.FileName);
        if (!System.IO.File.Exists(filePath)) return NotFound();

        return PhysicalFile(filePath, "application/octet-stream", meta.FileName);
    }
}
