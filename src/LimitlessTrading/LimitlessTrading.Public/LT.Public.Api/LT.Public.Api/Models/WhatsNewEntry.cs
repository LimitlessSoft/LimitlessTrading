namespace LT.Public.Api.Models;

public class WhatsNewEntry
{
    public int Id { get; set; }
    public string Version { get; set; } = "";
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public DateTime PublishedAt { get; set; }
    public string[] Tags { get; set; } = [];
}
