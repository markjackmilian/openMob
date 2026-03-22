using System.Collections.Concurrent;

namespace openMob.Core.Services;

/// <summary>
/// In-memory draft storage for message composition state.
/// Keyed by session ID. Drafts are lost on app restart.
/// </summary>
internal sealed class DraftService : IDraftService
{
    private readonly ConcurrentDictionary<string, string> _drafts = new();
    private readonly ConcurrentDictionary<string, ComposerDraft> _composerDrafts = new();

    /// <inheritdoc />
    public string? GetDraft(string sessionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        return _drafts.GetValueOrDefault(sessionId);
    }

    /// <inheritdoc />
    public void SaveDraft(string sessionId, string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentNullException.ThrowIfNull(text);
        _drafts[sessionId] = text;
    }

    /// <inheritdoc />
    public void ClearDraft(string sessionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        _drafts.TryRemove(sessionId, out _);
    }

    /// <inheritdoc />
    public ComposerDraft? GetComposerDraft(string sessionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        return _composerDrafts.GetValueOrDefault(sessionId);
    }

    /// <inheritdoc />
    public void SaveComposerDraft(string sessionId, ComposerDraft draft)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentNullException.ThrowIfNull(draft);
        _composerDrafts[sessionId] = draft;
    }

    /// <inheritdoc />
    public void ClearComposerDraft(string sessionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        _composerDrafts.TryRemove(sessionId, out _);
    }
}
