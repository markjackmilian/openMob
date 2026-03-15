namespace openMob.Core.Infrastructure.Security;

/// <summary>
/// Abstracts secure credential storage for server connections.
/// Implementations use platform-specific secure storage (e.g., MAUI SecureStorage).
/// </summary>
/// <remarks>
/// This interface lives in openMob.Core with zero MAUI dependencies.
/// The concrete implementation (<c>MauiServerCredentialStore</c>) resides in the MAUI project
/// and is registered in <c>MauiProgram.cs</c>.
/// <para>
/// Platform behaviour:
/// <list type="bullet">
///   <item><description>iOS — backed by the Keychain.</description></item>
///   <item><description>Android — backed by EncryptedSharedPreferences (Android Keystore).</description></item>
/// </list>
/// </para>
/// </remarks>
public interface IServerCredentialStore
{
    /// <summary>Saves a password for the given connection ID.</summary>
    /// <param name="connectionId">The unique identifier of the server connection.</param>
    /// <param name="password">The password to store securely.</param>
    Task SavePasswordAsync(string connectionId, string password);

    /// <summary>Retrieves the password for the given connection ID, or null if not found.</summary>
    /// <param name="connectionId">The unique identifier of the server connection.</param>
    /// <returns>The stored password, or <c>null</c> if no password exists for the given ID.</returns>
    Task<string?> GetPasswordAsync(string connectionId);

    /// <summary>Deletes the password for the given connection ID. No-op if not found (idempotent).</summary>
    /// <param name="connectionId">The unique identifier of the server connection.</param>
    Task DeletePasswordAsync(string connectionId);
}
