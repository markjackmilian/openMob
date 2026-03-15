namespace openMob.Core.Infrastructure.Settings;

/// <summary>
/// Provides access to user-configurable settings for the opencode API client.
/// </summary>
/// <remarks>
/// The interface lives in <c>openMob.Core</c> with zero MAUI dependencies.
/// The concrete implementation (<c>MauiOpencodeSettingsService</c>) resides in the MAUI project
/// and is registered in <c>MauiProgram.cs</c>.
/// </remarks>
public interface IOpencodeSettingsService
{
    /// <summary>
    /// Gets the configured HTTP request timeout in seconds.
    /// </summary>
    /// <returns>The timeout in seconds. Defaults to <c>120</c> if not explicitly set.</returns>
    int GetTimeoutSeconds();

    /// <summary>
    /// Persists the HTTP request timeout setting.
    /// </summary>
    /// <param name="value">The timeout in seconds to store.</param>
    /// <param name="ct">Cancellation token.</param>
    Task SetTimeoutSecondsAsync(int value, CancellationToken ct = default);
}
