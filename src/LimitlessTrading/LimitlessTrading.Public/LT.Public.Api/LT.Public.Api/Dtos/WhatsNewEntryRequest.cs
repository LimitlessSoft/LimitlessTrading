namespace LT.Public.Api.Dtos;

public class WhatsNewEntryRequest
{
    public string Version { get; set; } = "";
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public DateTime PublishedAt { get; set; }
    public string[] Tags { get; set; } = [];
}
