using LT.Public.Api.Data;
using LT.Public.Api.Dtos;
using LT.Public.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LT.Public.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class FeedbackController(AppDbContext db, IConfiguration config) : ControllerBase
{
    private string? UserEmail => Request.Headers.TryGetValue("X-User-Email", out var v) ? v.ToString() : null;

    private bool IsAdmin()
    {
        var email = UserEmail;
        if (string.IsNullOrWhiteSpace(email)) return false;
        var admins = config["Feedback:AdminEmails"] ?? "";
        return admins.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Contains(email, StringComparer.OrdinalIgnoreCase);
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? category, [FromQuery] string? status, [FromQuery] string? sort, [FromQuery] string? search)
    {
        var email = UserEmail ?? "";
        var q = db.Feedback.AsQueryable();

        if (!string.IsNullOrWhiteSpace(category))
            q = q.Where(f => f.Category == category);
        if (!string.IsNullOrWhiteSpace(status))
            q = q.Where(f => f.Status == status);
        if (!string.IsNullOrWhiteSpace(search))
            q = q.Where(f => EF.Functions.ILike(f.Title, $"%{search}%") || EF.Functions.ILike(f.Description, $"%{search}%"));

        q = sort == "votes"
            ? q.OrderByDescending(f => f.IsPinned).ThenByDescending(f => f.Upvotes)
            : q.OrderByDescending(f => f.IsPinned).ThenByDescending(f => f.CreatedAt);

        var upvotedIds = await db.FeedbackUpvotes
            .Where(u => u.UserId == email)
            .Select(u => u.FeedbackId)
            .ToListAsync();

        var items = await q.Select(f => new
        {
            f.Id, f.Title, f.Description, f.Category, f.Status,
            f.AuthorId, f.Upvotes, f.IsPinned, f.CreatedAt, f.UpdatedAt,
            HasUpvoted = upvotedIds.Contains(f.Id)
        }).ToListAsync();

        return Ok(items);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateFeedbackRequest req)
    {
        var email = UserEmail;
        if (string.IsNullOrWhiteSpace(email))
            return Unauthorized(new { error = "Not authenticated" });

        var validCategories = new[] { "feature", "bug", "improvement" };
        if (!validCategories.Contains(req.Category))
            return BadRequest(new { error = "Invalid category" });

        var item = new Feedback
        {
            Id = Guid.NewGuid(),
            Title = req.Title.Trim(),
            Description = req.Description.Trim(),
            Category = req.Category,
            Status = "open",
            AuthorId = email,
            Upvotes = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.Feedback.Add(item);
        await db.SaveChangesAsync();
        return Ok(new { item.Id, item.Title, item.Description, item.Category, item.Status, item.AuthorId, item.Upvotes, item.CreatedAt, item.UpdatedAt, HasUpvoted = false });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var email = UserEmail ?? "";
        var item = await db.Feedback.FindAsync(id);
        if (item is null) return NotFound();

        var hasUpvoted = await db.FeedbackUpvotes.AnyAsync(u => u.FeedbackId == id && u.UserId == email);

        var allComments = await db.FeedbackComments
            .Where(c => c.FeedbackId == id)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync();

        var topLevel = allComments
            .Where(c => c.ParentCommentId is null)
            .Select(c => BuildComment(c, allComments))
            .ToList();

        return Ok(new
        {
            item.Id, item.Title, item.Description, item.Category, item.Status,
            item.AuthorId, item.Upvotes, item.IsPinned, item.CreatedAt, item.UpdatedAt,
            HasUpvoted = hasUpvoted,
            Comments = topLevel
        });
    }

    private static object BuildComment(FeedbackComment c, List<FeedbackComment> all) => new
    {
        c.Id, c.FeedbackId, c.ParentCommentId, c.AuthorId, c.IsOfficialResponse, c.Body, c.CreatedAt,
        Replies = all.Where(r => r.ParentCommentId == c.Id).Select(r => BuildComment(r, all)).ToList()
    };

    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateFeedbackRequest req)
    {
        if (!IsAdmin()) return Forbid();

        var item = await db.Feedback.FindAsync(id);
        if (item is null) return NotFound();

        var validStatuses = new[] { "open", "in-review", "planned", "done", "rejected" };
        if (!validStatuses.Contains(req.Status))
            return BadRequest(new { error = "Invalid status" });

        item.Status = req.Status;
        item.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Ok(new { item.Id, item.Status, item.UpdatedAt });
    }

    [HttpPost("{id:guid}/upvote")]
    public async Task<IActionResult> ToggleUpvote(Guid id)
    {
        var email = UserEmail;
        if (string.IsNullOrWhiteSpace(email))
            return Unauthorized(new { error = "Not authenticated" });

        var item = await db.Feedback.FindAsync(id);
        if (item is null) return NotFound();

        var existing = await db.FeedbackUpvotes.FirstOrDefaultAsync(u => u.FeedbackId == id && u.UserId == email);
        if (existing is not null)
        {
            db.FeedbackUpvotes.Remove(existing);
            item.Upvotes = Math.Max(0, item.Upvotes - 1);
            await db.SaveChangesAsync();
            return Ok(new { upvotes = item.Upvotes, hasUpvoted = false });
        }

        db.FeedbackUpvotes.Add(new FeedbackUpvote { Id = Guid.NewGuid(), FeedbackId = id, UserId = email, CreatedAt = DateTime.UtcNow });
        item.Upvotes++;
        await db.SaveChangesAsync();
        return Ok(new { upvotes = item.Upvotes, hasUpvoted = true });
    }

    [HttpPost("{id:guid}/comments")]
    public async Task<IActionResult> AddComment(Guid id, [FromBody] AddCommentRequest req)
    {
        var email = UserEmail;
        if (string.IsNullOrWhiteSpace(email))
            return Unauthorized(new { error = "Not authenticated" });

        if (!await db.Feedback.AnyAsync(f => f.Id == id))
            return NotFound();

        var comment = new FeedbackComment
        {
            Id = Guid.NewGuid(),
            FeedbackId = id,
            ParentCommentId = req.ParentCommentId,
            AuthorId = email,
            IsOfficialResponse = false,
            Body = req.Body.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        db.FeedbackComments.Add(comment);
        await db.SaveChangesAsync();
        return Ok(new { comment.Id, comment.FeedbackId, comment.ParentCommentId, comment.AuthorId, comment.IsOfficialResponse, comment.Body, comment.CreatedAt, Replies = Array.Empty<object>() });
    }

    [HttpPost("{id:guid}/pin")]
    public async Task<IActionResult> TogglePin(Guid id)
    {
        if (!IsAdmin()) return Forbid();

        var item = await db.Feedback.FindAsync(id);
        if (item is null) return NotFound();

        item.IsPinned = !item.IsPinned;
        item.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Ok(new { item.Id, item.IsPinned });
    }

    [HttpPatch("{id:guid}/comments/{commentId:guid}")]
    public async Task<IActionResult> UpdateComment(Guid id, Guid commentId, [FromBody] UpdateCommentRequest req)
    {
        if (!IsAdmin()) return Forbid();

        var comment = await db.FeedbackComments.FirstOrDefaultAsync(c => c.Id == commentId && c.FeedbackId == id);
        if (comment is null) return NotFound();

        comment.IsOfficialResponse = req.IsOfficialResponse;
        await db.SaveChangesAsync();
        return Ok(new { comment.Id, comment.IsOfficialResponse });
    }
}
