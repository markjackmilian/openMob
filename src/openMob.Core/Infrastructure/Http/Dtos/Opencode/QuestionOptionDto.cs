using System.Text.Json.Serialization;

namespace openMob.Core.Infrastructure.Http.Dtos.Opencode;

/// <summary>
/// Represents a single answer option within a <see cref="QuestionInfoDto"/>.
/// </summary>
/// <param name="Label">Display text for the option.</param>
/// <param name="Description">Explanation of what the option does.</param>
public sealed record QuestionOptionDto(
    [property: JsonPropertyName("label")] string Label,
    [property: JsonPropertyName("description")] string Description
);
