namespace openMob.Core.Infrastructure.Http.Dtos;

/// <summary>Data transfer object representing a server-sent event from the opencode server.</summary>
public sealed record ServerEventDto(
    string EventType,
    string? Payload
);
