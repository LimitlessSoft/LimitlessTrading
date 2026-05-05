namespace LT.Public.Api.Models;

public class FeedbackComment
{
    public Guid Id { get; set; }
    public Guid FeedbackId { get; set; }
    public Feedback Feedback { get; set; } = null!;
    public Guid? ParentCommentId { get; set; }
    public string AuthorId { get; set; } = "";
    public bool IsOfficialResponse { get; set; }
    public string Body { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}
