using openMob.Core.Models;

namespace openMob.Core.Services;

/// <summary>
/// In-memory draft storage for message composition state.
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

    /// <summary>Gets the full composer state for a session, or <c>null</c> if none exists.</summary>
    ComposerDraft? GetComposerDraft(string sessionId);

    /// <summary>Saves the full composer state for a session.</summary>
    void SaveComposerDraft(string sessionId, ComposerDraft draft);

    /// <summary>Removes the full composer state for a session.</summary>
    void ClearComposerDraft(string sessionId);
}

/// <summary>
/// Captures the full state of the message composer for draft persistence.
/// </summary>
/// <param name="Text">The message text.</param>
/// <param name="AgentName">The selected agent name override, or <c>null</c>.</param>
/// <param name="ModelId">The selected model ID override, or <c>null</c>.</param>
/// <param name="ThinkingLevel">The selected thinking level.</param>
/// <param name="AutoAccept">Whether auto-accept is enabled.</param>
public sealed record ComposerDraft(
    string Text,
    string? AgentName,
    string? ModelId,
    ThinkingLevel ThinkingLevel,
    bool AutoAccept
);
