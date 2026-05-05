namespace LT.Public.Api.Dtos;
public record AddCommentRequest(Guid? ParentCommentId, string Body);
