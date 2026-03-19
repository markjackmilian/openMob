namespace openMob.Core.Models;

/// <summary>
/// Represents a command available in the Command Palette.
/// Mapped from <see cref="Infrastructure.Http.Dtos.Opencode.CommandDto"/>.
/// </summary>
/// <param name="Name">The command name (slash-command identifier).</param>
/// <param name="Description">Optional human-readable description.</param>
/// <param name="IsSubtask">Whether this command runs as a subtask.</param>
public sealed record CommandItem(
    string Name,
    string? Description,
    bool IsSubtask
);
