namespace openMob.Tests.Helpers;

/// <summary>
/// Factory helpers for creating test data objects.
/// Add static factory methods here as features are implemented.
/// </summary>
public static class TestDataBuilder
{
    /// <summary>Creates a sample <see cref="SessionDto"/> for use in tests.</summary>
    public static SessionDto CreateSession(
        string id = "test-session-1",
        string? title = "Test Session",
        DateTimeOffset? createdAt = null)
        => new(id, title, createdAt ?? DateTimeOffset.UtcNow);

    /// <summary>Creates a sample <see cref="MessageDto"/> for use in tests.</summary>
    public static MessageDto CreateMessage(
        string id = "test-message-1",
        string sessionId = "test-session-1",
        string content = "Hello",
        string role = "user",
        DateTimeOffset? createdAt = null)
        => new(id, sessionId, content, role, createdAt ?? DateTimeOffset.UtcNow);
}
