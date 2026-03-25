namespace openMob.Core.Services;

/// <summary>
/// Represents a project file for the file picker.
/// </summary>
/// <param name="RelativePath">The file path relative to the project root.</param>
/// <param name="Name">The file display name.</param>
/// <param name="Type">The file type hint, or <c>null</c>.</param>
/// <param name="IsIgnored">Whether the entry is ignored by the server.</param>
public sealed record FileDto(string RelativePath, string Name, string? Type = null, bool IsIgnored = false);
