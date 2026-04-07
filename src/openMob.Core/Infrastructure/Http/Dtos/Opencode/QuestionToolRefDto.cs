using System.Text.Json.Serialization;

namespace openMob.Core.Infrastructure.Http.Dtos.Opencode;

/// <summary>
/// References the tool call that triggered a question, used for correlation with tool call cards.
/// </summary>
/// <param name="MessageId">The message identifier containing the tool call.</param>
/// <param name="CallId">The tool call identifier for suppression correlation.</param>
public sealed record QuestionToolRefDto(
    [property: JsonPropertyName("messageID")] string MessageId,
    [property: JsonPropertyName("callID")] string CallId
);
