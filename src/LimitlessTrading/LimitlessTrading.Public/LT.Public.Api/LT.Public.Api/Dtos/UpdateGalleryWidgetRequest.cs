using System.Text.Json;

namespace LT.Public.Api.Dtos;

public record UpdateGalleryWidgetRequest(string Name, string? Description, JsonElement? Config);
