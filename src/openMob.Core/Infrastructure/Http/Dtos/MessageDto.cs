namespace openMob.Core.Infrastructure.Http.Dtos;

/// <summary>Data transfer object representing a message within a session.</summary>
public sealed record MessageDto(
    string Id,
    string SessionId,
    string Content,
    string Role,
    DateTimeOffset CreatedAt
);
