using System.Text.Json;
using System.Text.Json.Serialization;

namespace openMob.Core.Infrastructure.Http.Dtos.Opencode;

/// <summary>
/// Represents a pending TUI control request returned by <c>GET /tui/control/next</c>.
/// The <see cref="Body"/> element contains the control-type-specific payload and must
/// be parsed downstream based on the value of <see cref="Type"/>.
/// </summary>
/// <param name="Id">The unique control request identifier.</param>
/// <param name="SessionId">The session identifier this control request belongs to.</param>
/// <param name="Type">The control type (e.g. <c>"question"</c>).</param>
/// <param name="Body">The raw control-type-specific payload as a JSON element.</param>
public sealed record TuiControlRequestDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("sessionID")] string SessionId,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("body")] JsonElement Body
);
