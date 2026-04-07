using System.Text.Json.Serialization;

namespace openMob.Core.Infrastructure.Http.Dtos.Opencode;

/// <summary>
/// Represents a single question within a <see cref="QuestionRequestDto"/>.
/// </summary>
/// <param name="Question">The question text to display.</param>
/// <param name="Header">Short label for the question.</param>
/// <param name="Options">Available answer choices.</param>
/// <param name="Multiple">Whether multiple selections are allowed (parsed but ignored in v1).</param>
/// <param name="Custom">Whether free-text input is allowed. Defaults to <c>true</c> when absent.</param>
public sealed record QuestionInfoDto(
    [property: JsonPropertyName("question")] string Question,
    [property: JsonPropertyName("header")] string Header,
    [property: JsonPropertyName("options")] IReadOnlyList<QuestionOptionDto> Options,
    [property: JsonPropertyName("multiple")] bool? Multiple = null,
    [property: JsonPropertyName("custom")] bool? Custom = null
);
