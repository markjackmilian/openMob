using System.Text.Json;
using System.Text.Json.Serialization;

namespace openMob.Core.Infrastructure.Http.Dtos.Opencode.Requests;

/// <summary>Request body for <c>POST /log</c>.</summary>
/// <param name="Level">The log level (e.g. <c>DEBUG</c>, <c>INFO</c>, <c>WARN</c>, <c>ERROR</c>).</param>
/// <param name="Message">The log message.</param>
/// <param name="Extra">Optional additional structured data as raw JSON.</param>
public sealed record WriteLogRequest(
    [property: JsonPropertyName("level")] string Level,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("extra")] JsonElement? Extra
);
