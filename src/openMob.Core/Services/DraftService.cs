using System.Collections.Concurrent;

namespace openMob.Core.Services;

/// <summary>
/// In-memory draft storage for message text.
/// Keyed by session ID. Drafts are lost on app restart.
/// </summary>
internal sealed class DraftService : IDraftService
{
    private readonly ConcurrentDictionary<string, string> _drafts = new();

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
}
