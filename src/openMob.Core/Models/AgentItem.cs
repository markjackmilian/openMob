namespace openMob.Core.Models;

/// <summary>
/// Display model for an agent entry in the agent picker UI.
/// </summary>
/// <param name="Name">The agent name, or <c>null</c> for the "Default" entry.</param>
/// <param name="Description">An optional description of the agent.</param>
/// <param name="IsSelected">Whether this agent is currently selected.</param>
public sealed record AgentItem(string? Name, string? Description, bool IsSelected)
{
    /// <summary>
    /// Gets the display name for this entry.
    /// Returns <c>"Default"</c> when <see cref="Name"/> is <c>null</c>.
    /// </summary>
    public string DisplayName => Name ?? "Default";
}
