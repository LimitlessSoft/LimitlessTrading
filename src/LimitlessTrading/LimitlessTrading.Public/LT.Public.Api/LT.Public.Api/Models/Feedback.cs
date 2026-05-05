namespace LT.Public.Api.Models;

public class Feedback
{
    public Guid Id { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Category { get; set; } = "feature"; // feature, bug, improvement
    public string Status { get; set; } = "open"; // open, in-review, planned, done, rejected
    public string AuthorId { get; set; } = "";
    public int Upvotes { get; set; }
    public bool IsPinned { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public ICollection<FeedbackUpvote> FeedbackUpvotes { get; set; } = [];
    public ICollection<FeedbackComment> Comments { get; set; } = [];
}
