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

    /// <summary>A pending permission request has been replied to (server-side auto-approval, rejection, or reply from another client).</summary>
    PermissionReplied,

    /// <summary>The AI is asking the user a question via the TUI control mechanism.</summary>
    QuestionRequested,

    /// <summary>A delta (incremental text chunk) for a message part was received during streaming.</summary>
    MessagePartDelta,

    /// <summary>A message was removed from a session.</summary>
    MessageRemoved,

    /// <summary>A specific part of a message was removed.</summary>
    MessagePartRemoved,

    /// <summary>A new session was created on the server.</summary>
    SessionCreated,

    /// <summary>A session was deleted on the server.</summary>
    SessionDeleted,

    /// <summary>An unrecognised event type was received; raw payload is preserved.</summary>
    Unknown,
}
