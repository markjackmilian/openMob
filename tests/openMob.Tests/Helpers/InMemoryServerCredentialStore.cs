using openMob.Core.Infrastructure.Security;

namespace openMob.Tests.Helpers;

/// <summary>In-memory implementation of <see cref="IServerCredentialStore"/> for testing.</summary>
internal sealed class InMemoryServerCredentialStore : IServerCredentialStore
{
    private readonly Dictionary<string, string> _store = new();

    /// <inheritdoc />
    public Task SavePasswordAsync(string connectionId, string password, CancellationToken cancellationToken = default)
    {
        _store[connectionId] = password;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<string?> GetPasswordAsync(string connectionId, CancellationToken cancellationToken = default)
    {
        _store.TryGetValue(connectionId, out var password);
        return Task.FromResult(password);
    }

    /// <inheritdoc />
    public Task DeletePasswordAsync(string connectionId, CancellationToken cancellationToken = default)
    {
        _store.Remove(connectionId);
        return Task.CompletedTask;
    }
}
