using openMob.Core.Infrastructure.Security;

namespace openMob.Infrastructure.Security;

/// <summary>
/// MAUI implementation of <see cref="IServerCredentialStore"/>.
/// Uses <see cref="SecureStorage"/> to persist server passwords on the device.
/// </summary>
/// <remarks>
/// Key format: <c>opencode_server_pwd_{connectionId}</c>.
/// <para>
/// Platform behaviour:
/// <list type="bullet">
///   <item><description>iOS — backed by the Keychain (encrypted at rest, backed up by iCloud if enabled).</description></item>
///   <item><description>Android — backed by EncryptedSharedPreferences (Android Keystore).</description></item>
/// </list>
/// </para>
/// </remarks>
internal sealed class MauiServerCredentialStore : IServerCredentialStore
{
    /// <summary>Prefix used for all SecureStorage keys related to server passwords.</summary>
    private const string KeyPrefix = "opencode_server_pwd_";

    /// <inheritdoc />
    public async Task SavePasswordAsync(string connectionId, string password, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        var key = BuildKey(connectionId);
        await SecureStorage.Default.SetAsync(key, password).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<string?> GetPasswordAsync(string connectionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionId);

        var key = BuildKey(connectionId);

        // On iOS, SecureStorage.GetAsync can throw if the Keychain is temporarily
        // unavailable (e.g. device locked, missing entitlement, first boot before first unlock).
        // Treat any exception as "no password stored" — the caller handles null gracefully.
        try
        {
            return await SecureStorage.Default.GetAsync(key).ConfigureAwait(false);
        }
        catch
        {
            return null;
        }
    }

    /// <inheritdoc />
    public Task DeletePasswordAsync(string connectionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionId);

        var key = BuildKey(connectionId);
        SecureStorage.Default.Remove(key);
        return Task.CompletedTask;
    }

    /// <summary>Builds the SecureStorage key for the given connection ID.</summary>
    /// <param name="connectionId">The unique identifier of the server connection.</param>
    /// <returns>The formatted key string.</returns>
    private static string BuildKey(string connectionId) => $"{KeyPrefix}{connectionId}";
}
