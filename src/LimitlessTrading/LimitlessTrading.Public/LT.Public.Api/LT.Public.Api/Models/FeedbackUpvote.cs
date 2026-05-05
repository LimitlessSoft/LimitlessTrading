namespace LT.Public.Api.Models;

public class FeedbackUpvote
{
    public Guid Id { get; set; }
    public Guid FeedbackId { get; set; }
    public Feedback Feedback { get; set; } = null!;
    public string UserId { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}
