using System.Text.Json;
using System.Text.Json.Serialization;

namespace openMob.Core.Infrastructure.Http.Dtos.Opencode;

/// <summary>
/// Represents a text search match. Maps the opencode text search result shape.
/// </summary>
/// <param name="File">The file path where the match was found.</param>
/// <param name="Line">The matched line content.</param>
/// <param name="LineNumber">The 1-based line number of the match.</param>
public sealed record TextMatchDto(
    [property: JsonPropertyName("file")] string File,
    [property: JsonPropertyName("line")] string Line,
    [property: JsonPropertyName("lineNumber")] int LineNumber
);

/// <summary>
/// Represents a position within a file (line and character offset).
/// </summary>
/// <param name="Line">The 0-based line number.</param>
/// <param name="Character">The 0-based character offset within the line.</param>
public sealed record RangePositionDto(
    [property: JsonPropertyName("line")] int Line,
    [property: JsonPropertyName("character")] int Character
);

/// <summary>
/// Represents a range within a file defined by start and end positions.
/// Maps the <c>Range</c> TypeScript type.
/// </summary>
/// <param name="Start">The start position of the range.</param>
/// <param name="End">The end position of the range.</param>
public sealed record RangeDto(
    [property: JsonPropertyName("start")] RangePositionDto Start,
    [property: JsonPropertyName("end")] RangePositionDto End
);

/// <summary>
/// Represents the location of a symbol within a file.
/// </summary>
/// <param name="Uri">The file URI.</param>
/// <param name="Range">The range within the file where the symbol is defined.</param>
public sealed record SymbolLocationDto(
    [property: JsonPropertyName("uri")] string Uri,
    [property: JsonPropertyName("range")] RangeDto Range
);

/// <summary>
/// Represents a code symbol found by the symbol search. Maps the <c>Symbol</c> TypeScript type.
/// </summary>
/// <param name="Name">The symbol name.</param>
/// <param name="Kind">The LSP symbol kind integer (e.g. 5 = Class, 6 = Method).</param>
/// <param name="Location">The location of the symbol in the file system.</param>
public sealed record SymbolDto(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("kind")] int Kind,
    [property: JsonPropertyName("location")] SymbolLocationDto Location
);

/// <summary>
/// Represents a node in the file tree. Maps the <c>FileNode</c> TypeScript type.
/// </summary>
/// <param name="Name">The file or directory name.</param>
/// <param name="Path">The relative path from the project root.</param>
/// <param name="Absolute">The absolute file system path.</param>
/// <param name="Type">The node type: <c>file</c> or <c>directory</c>.</param>
/// <param name="Ignored">Whether this node is ignored by the VCS.</param>
public sealed record FileNodeDto(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("absolute")] string Absolute,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("ignored")] bool Ignored
);

/// <summary>
/// Represents the content of a file. Maps the <c>FileContent</c> TypeScript type.
/// </summary>
/// <param name="Type">The content type: <c>text</c> or <c>binary</c>.</param>
/// <param name="Content">The file content (base64-encoded for binary files).</param>
/// <param name="Diff">A unified diff string, or <c>null</c>.</param>
/// <param name="Patch">The structured patch object as raw JSON (complex nested shape), or <c>null</c>.</param>
/// <param name="Encoding">The encoding, e.g. <c>base64</c>, or <c>null</c> for text files.</param>
/// <param name="MimeType">The MIME type of the file, or <c>null</c>.</param>
public sealed record FileContentDto(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("content")] string Content,
    [property: JsonPropertyName("diff")] string? Diff,
    [property: JsonPropertyName("patch")] JsonElement? Patch,
    [property: JsonPropertyName("encoding")] string? Encoding,
    [property: JsonPropertyName("mimeType")] string? MimeType
);

/// <summary>
/// Represents the VCS status of a file. Maps the <c>File</c> TypeScript type.
/// </summary>
/// <param name="Path">The file path relative to the project root.</param>
/// <param name="Added">Number of lines added.</param>
/// <param name="Removed">Number of lines removed.</param>
/// <param name="Status">The VCS status: <c>added</c>, <c>deleted</c>, or <c>modified</c>.</param>
public sealed record FileStatusDto(
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("added")] int Added,
    [property: JsonPropertyName("removed")] int Removed,
    [property: JsonPropertyName("status")] string Status
);
