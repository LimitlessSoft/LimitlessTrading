namespace LT.Public.Api.Models;

public record WidgetEntry(
    string Type,
    string Name,
    string Description,
    string[] Tags,
    string PreviewImageUrl,
    object ConfigSnippet
);
