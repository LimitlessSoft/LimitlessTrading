using System.Text.Json;
using LT.Public.Api.Data;
using LT.Public.Api.Dtos;
using LT.Public.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LT.Public.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GalleryController(AppDbContext db) : ControllerBase
{
    private string? UserEmail => Request.Headers.TryGetValue("X-User-Email", out var v) ? v.ToString() : null;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? type)
    {
        var q = db.GalleryWidgets.AsQueryable();
        if (!string.IsNullOrWhiteSpace(type))
            q = q.Where(w => w.WidgetType == type);

        var rows = await q
            .OrderByDescending(w => w.CreatedAt)
            .Select(w => new { w.Id, w.Name, w.Description, w.WidgetType, w.AuthorId, w.CreatedAt, w.ConfigJson })
            .ToListAsync();

        var items = rows.Select(w => new
        {
            w.Id, w.Name, w.Description, w.WidgetType, w.AuthorId, w.CreatedAt,
            Config = JsonSerializer.Deserialize<object>(w.ConfigJson)
        });

        return Ok(items);
    }

    [HttpPost]
    public async Task<IActionResult> Share([FromBody] ShareGalleryWidgetRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Name))
            return BadRequest(new { error = "Name is required" });

        string configJson;
        try
        {
            configJson = JsonSerializer.Serialize(req.Config);
        }
        catch
        {
            return BadRequest(new { error = "Invalid config" });
        }

        var entry = new GalleryWidget
        {
            Id = Guid.NewGuid(),
            Name = req.Name.Trim(),
            Description = req.Description?.Trim() ?? "",
            WidgetType = req.WidgetType?.Trim() ?? "",
            ConfigJson = configJson,
            AuthorId = UserEmail ?? "anonymous",
            CreatedAt = DateTime.UtcNow
        };

        db.GalleryWidgets.Add(entry);
        await db.SaveChangesAsync();

        return Ok(new
        {
            entry.Id, entry.Name, entry.Description, entry.WidgetType,
            entry.AuthorId, entry.CreatedAt,
            Config = req.Config
        });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateGalleryWidgetRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Name))
            return BadRequest(new { error = "Name is required" });

        var email = UserEmail;
        var entry = await db.GalleryWidgets.FindAsync(id);
        if (entry is null) return NotFound();
        if (entry.AuthorId != email) return Forbid();

        entry.Name = req.Name.Trim();
        entry.Description = req.Description?.Trim() ?? "";
        if (req.Config.HasValue && req.Config.Value.ValueKind != JsonValueKind.Null)
            entry.ConfigJson = JsonSerializer.Serialize(req.Config.Value);
        await db.SaveChangesAsync();

        return Ok(new { entry.Id, entry.Name, entry.Description, entry.WidgetType, entry.AuthorId, entry.CreatedAt });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var email = UserEmail;
        var entry = await db.GalleryWidgets.FindAsync(id);
        if (entry is null) return NotFound();
        if (entry.AuthorId != email) return Forbid();

        db.GalleryWidgets.Remove(entry);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
