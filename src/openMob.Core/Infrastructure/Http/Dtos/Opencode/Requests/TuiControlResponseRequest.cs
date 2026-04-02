using System.Text.Json.Serialization;

namespace openMob.Core.Infrastructure.Http.Dtos.Opencode.Requests;

/// <summary>Request body for <c>POST /tui/control/response</c>.</summary>
/// <param name="RequestId">The TUI control request identifier.</param>
/// <param name="Body">The answer text.</param>
public sealed record TuiControlResponseRequest(
    [property: JsonPropertyName("requestID")] string RequestId,
    [property: JsonPropertyName("body")] string Body
);
