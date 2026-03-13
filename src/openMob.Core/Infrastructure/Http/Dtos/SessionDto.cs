namespace openMob.Core.Infrastructure.Http.Dtos;

/// <summary>Data transfer object representing a conversation session.</summary>
public sealed record SessionDto(
    string Id,
    string? Title,
    DateTimeOffset CreatedAt
);
