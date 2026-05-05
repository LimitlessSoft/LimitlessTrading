using LT.Public.Api.Data;
using LT.Public.Api.Dtos;
using LT.Public.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LT.Public.Api.Controllers;

[ApiController]
[Route("api/whats-new")]
public class WhatsNewController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int limit = 10, [FromQuery] int offset = 0)
    {
        limit = Math.Clamp(limit, 1, 50);

        var items = await db.WhatsNewEntries
            .OrderByDescending(e => e.PublishedAt)
            .Skip(offset)
            .Take(limit)
            .Select(e => new { e.Id, e.Version, e.Title, e.Body, e.PublishedAt, e.Tags })
            .ToListAsync();

        return Ok(items);
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Create([FromBody] WhatsNewEntryRequest req)
    {
        var entry = new WhatsNewEntry
        {
            Version = req.Version.Trim(),
            Title = req.Title.Trim(),
            Body = req.Body.Trim(),
            PublishedAt = req.PublishedAt,
            Tags = req.Tags
        };

        db.WhatsNewEntries.Add(entry);
        await db.SaveChangesAsync();

        return Ok(new { entry.Id, entry.Version, entry.Title, entry.Body, entry.PublishedAt, entry.Tags });
    }

    [HttpPut("{id:int}")]
    [Authorize]
    public async Task<IActionResult> Update(int id, [FromBody] WhatsNewEntryRequest req)
    {
        var entry = await db.WhatsNewEntries.FindAsync(id);
        if (entry is null) return NotFound();

        entry.Version = req.Version.Trim();
        entry.Title = req.Title.Trim();
        entry.Body = req.Body.Trim();
        entry.PublishedAt = req.PublishedAt;
        entry.Tags = req.Tags;

        await db.SaveChangesAsync();

        return Ok(new { entry.Id, entry.Version, entry.Title, entry.Body, entry.PublishedAt, entry.Tags });
    }

    [HttpDelete("{id:int}")]
    [Authorize]
    public async Task<IActionResult> Delete(int id)
    {
        var entry = await db.WhatsNewEntries.FindAsync(id);
        if (entry is null) return NotFound();

        db.WhatsNewEntries.Remove(entry);
        await db.SaveChangesAsync();

        return NoContent();
    }
}
