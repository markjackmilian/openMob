namespace openMob.Core.Services;

/// <summary>
/// In-memory draft storage for message text.
/// Keyed by session ID. Drafts are lost on app restart.
/// </summary>
public interface IDraftService
{
    /// <summary>Gets the draft text for a session, or <c>null</c> if none exists.</summary>
    string? GetDraft(string sessionId);

    /// <summary>Saves draft text for a session. Overwrites any existing draft.</summary>
    void SaveDraft(string sessionId, string text);

    /// <summary>Removes the draft for a session (called after sending).</summary>
    void ClearDraft(string sessionId);
}
