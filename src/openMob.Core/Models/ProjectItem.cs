namespace openMob.Core.Models;

/// <summary>
/// Display model for a project list item in the UI.
/// </summary>
/// <param name="Id">The unique project identifier.</param>
/// <param name="Name">The display name of the project (derived from the worktree directory name).</param>
/// <param name="Path">The full worktree path of the project.</param>
/// <param name="IsActive">Whether this is the currently active project.</param>
public sealed record ProjectItem(string Id, string Name, string Path, bool IsActive);
