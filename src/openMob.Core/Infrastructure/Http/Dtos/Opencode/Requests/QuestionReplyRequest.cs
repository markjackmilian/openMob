using System.Text.Json.Serialization;

namespace openMob.Core.Infrastructure.Http.Dtos.Opencode.Requests;

/// <summary>
/// Request body for <c>POST /question/{requestId}/reply</c>.
/// The outer array has one element per question (we always send one).
/// Each inner array contains the selected option labels.
/// </summary>
/// <param name="Answers">The nested answers array: <c>[["answer1", ...]]</c>.</param>
public sealed record QuestionReplyRequest(
    [property: JsonPropertyName("answers")] IReadOnlyList<IReadOnlyList<string>> Answers
);
