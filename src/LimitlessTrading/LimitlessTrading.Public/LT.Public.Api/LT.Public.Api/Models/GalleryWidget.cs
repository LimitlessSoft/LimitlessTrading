namespace LT.Public.Api.Models;

public class GalleryWidget
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string WidgetType { get; set; } = "";
    public string ConfigJson { get; set; } = "";
    public string AuthorId { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}
