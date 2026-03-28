namespace openMob.Core.Models;

/// <summary>
/// Identifies the sender category of a chat message.
/// </summary>
public enum SenderType
{
    /// <summary>The human user.</summary>
    User,

    /// <summary>The primary AI agent.</summary>
    Agent,

    /// <summary>A subagent invoked by the primary agent.</summary>
    Subagent,

    /// <summary>An unhandled SSE event rendered as a fallback diagnostic card.</summary>
    Fallback
}
