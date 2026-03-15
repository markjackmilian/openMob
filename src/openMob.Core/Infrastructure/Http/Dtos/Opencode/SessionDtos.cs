using System.Text.Json.Serialization;

namespace openMob.Core.Infrastructure.Http.Dtos.Opencode;

/// <summary>
/// Represents a file diff between two states. Maps the <c>FileDiff</c> TypeScript type.
/// </summary>
/// <param name="File">The file path that was changed.</param>
/// <param name="Before">The content before the change.</param>
/// <param name="After">The content after the change.</param>
/// <param name="Additions">Number of lines added.</param>
/// <param name="Deletions">Number of lines deleted.</param>
public sealed record FileDiffDto(
    [property: JsonPropertyName("file")] string File,
    [property: JsonPropertyName("before")] string Before,
    [property: JsonPropertyName("after")] string After,
    [property: JsonPropertyName("additions")] int Additions,
    [property: JsonPropertyName("deletions")] int Deletions
);

/// <summary>
/// Represents the time metadata for a session.
/// </summary>
/// <param name="Created">Unix timestamp (ms) when the session was created.</param>
/// <param name="Updated">Unix timestamp (ms) when the session was last updated.</param>
/// <param name="Compacting">Unix timestamp (ms) when compaction started, or <c>null</c>.</param>
public sealed record SessionTimeDto(
    [property: JsonPropertyName("created")] long Created,
    [property: JsonPropertyName("updated")] long Updated,
    [property: JsonPropertyName("compacting")] long? Compacting
);

/// <summary>
/// Represents the summary statistics for a session.
/// </summary>
/// <param name="Additions">Total lines added across all files.</param>
/// <param name="Deletions">Total lines deleted across all files.</param>
/// <param name="Files">Number of files changed.</param>
/// <param name="Diffs">Detailed per-file diffs, or <c>null</c> if not included.</param>
public sealed record SessionSummaryDto(
    [property: JsonPropertyName("additions")] int Additions,
    [property: JsonPropertyName("deletions")] int Deletions,
    [property: JsonPropertyName("files")] int Files,
    [property: JsonPropertyName("diffs")] IReadOnlyList<FileDiffDto>? Diffs
);

/// <summary>
/// Represents the share information for a session.
/// </summary>
/// <param name="Url">The public share URL.</param>
public sealed record SessionShareDto(
    [property: JsonPropertyName("url")] string Url
);

/// <summary>
/// Represents the revert state of a session.
/// </summary>
/// <param name="MessageId">The ID of the message to revert to.</param>
/// <param name="PartId">The ID of the part to revert to, or <c>null</c>.</param>
/// <param name="Snapshot">The snapshot identifier, or <c>null</c>.</param>
/// <param name="Diff">The diff content, or <c>null</c>.</param>
public sealed record SessionRevertDto(
    [property: JsonPropertyName("messageID")] string MessageId,
    [property: JsonPropertyName("partID")] string? PartId,
    [property: JsonPropertyName("snapshot")] string? Snapshot,
    [property: JsonPropertyName("diff")] string? Diff
);

/// <summary>
/// Response DTO for a session. Maps the <c>Session</c> TypeScript type.
/// </summary>
/// <param name="Id">The unique session identifier.</param>
/// <param name="ProjectId">The ID of the project this session belongs to.</param>
/// <param name="Directory">The working directory for this session.</param>
/// <param name="ParentId">The ID of the parent session, or <c>null</c> for root sessions.</param>
/// <param name="Summary">Session summary statistics, or <c>null</c>.</param>
/// <param name="Share">Share information, or <c>null</c> if not shared.</param>
/// <param name="Title">The session title.</param>
/// <param name="Version">The session version string.</param>
/// <param name="Time">Session timestamps.</param>
/// <param name="Revert">Revert state, or <c>null</c> if not reverted.</param>
public sealed record SessionDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("projectID")] string ProjectId,
    [property: JsonPropertyName("directory")] string Directory,
    [property: JsonPropertyName("parentID")] string? ParentId,
    [property: JsonPropertyName("summary")] SessionSummaryDto? Summary,
    [property: JsonPropertyName("share")] SessionShareDto? Share,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("time")] SessionTimeDto Time,
    [property: JsonPropertyName("revert")] SessionRevertDto? Revert
);

/// <summary>
/// Represents the status of a session. Maps the <c>SessionStatus</c> TypeScript union type.
/// </summary>
/// <param name="Type">The status type: <c>idle</c>, <c>busy</c>, or <c>retry</c>.</param>
/// <param name="Attempt">The retry attempt number (only present when <c>Type</c> is <c>retry</c>).</param>
/// <param name="Message">The retry message (only present when <c>Type</c> is <c>retry</c>).</param>
/// <param name="Next">Unix timestamp (ms) for the next retry (only present when <c>Type</c> is <c>retry</c>).</param>
public sealed record SessionStatusDto(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("attempt")] int? Attempt,
    [property: JsonPropertyName("message")] string? Message,
    [property: JsonPropertyName("next")] long? Next
);

/// <summary>
/// Represents a todo item within a session. Maps the <c>Todo</c> TypeScript type.
/// </summary>
/// <param name="Id">The unique identifier for this todo item.</param>
/// <param name="Content">Brief description of the task.</param>
/// <param name="Status">Current status: <c>pending</c>, <c>in_progress</c>, <c>completed</c>, or <c>cancelled</c>.</param>
/// <param name="Priority">Priority level: <c>high</c>, <c>medium</c>, or <c>low</c>.</param>
public sealed record TodoDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("content")] string Content,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("priority")] string Priority
);
