namespace openMob.Core.Helpers;

/// <summary>Utility methods for project display name extraction.</summary>
public static class ProjectNameHelper
{
    /// <summary>Extracts a display name from a worktree path (last directory segment).</summary>
    /// <param name="worktreePath">The full worktree path.</param>
    /// <returns>The last directory segment, or <c>"Unknown"</c> if the path is empty.</returns>
    public static string ExtractFromWorktree(string worktreePath)
    {
        if (string.IsNullOrWhiteSpace(worktreePath))
            return "Unknown";

        var trimmed = worktreePath.TrimEnd('/', '\\');
        var lastSep = trimmed.LastIndexOfAny(['/', '\\']);
        return lastSep >= 0 ? trimmed[(lastSep + 1)..] : trimmed;
    }
}
