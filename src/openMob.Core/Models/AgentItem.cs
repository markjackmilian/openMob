namespace openMob.Core.Models;

/// <summary>
/// Display model for an agent entry in the agent picker UI.
/// </summary>
/// <param name="Name">The agent name.</param>
/// <param name="Description">An optional description of the agent.</param>
/// <param name="IsSelected">Whether this agent is currently selected.</param>
public sealed record AgentItem(string Name, string? Description, bool IsSelected);
