using System.Text.Json.Serialization;

namespace openMob.Core.Infrastructure.Http.Dtos.Opencode;

/// <summary>
/// Represents a pending question request returned by <c>GET /question</c>.
/// </summary>
/// <param name="Id">The unique question request identifier.</param>
/// <param name="SessionId">The session identifier this question belongs to.</param>
/// <param name="Questions">The array of question objects.</param>
/// <param name="Tool">Optional tool reference for correlation with tool call cards.</param>
public sealed record QuestionRequestDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("sessionID")] string SessionId,
    [property: JsonPropertyName("questions")] IReadOnlyList<QuestionInfoDto> Questions,
    [property: JsonPropertyName("tool")] QuestionToolRefDto? Tool = null
);
