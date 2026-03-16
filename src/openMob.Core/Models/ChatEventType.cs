namespace openMob.Core.Models;

/// <summary>Discriminates the type of a chat SSE event.</summary>
public enum ChatEventType
{
    /// <summary>The SSE connection was established and the server sent the initial connected event.</summary>
    ServerConnected,

    /// <summary>A message was created or updated (streaming text or completion).</summary>
    MessageUpdated,

    /// <summary>A single part of a message was updated.</summary>
    MessagePartUpdated,

    /// <summary>Session metadata was updated.</summary>
    SessionUpdated,

    /// <summary>An error occurred during session processing.</summary>
    SessionError,

    /// <summary>The AI is requesting a permission (tool call approval).</summary>
    PermissionRequested,

    /// <summary>The state of a pending permission was updated.</summary>
    PermissionUpdated,

    /// <summary>An unrecognised event type was received; raw payload is preserved.</summary>
    Unknown,
}
