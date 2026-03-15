using openMob.Core.Infrastructure.Settings;

namespace openMob.Infrastructure.Settings;

/// <summary>
/// MAUI implementation of <see cref="IOpencodeSettingsService"/> backed by
/// <see cref="Microsoft.Maui.Storage.Preferences"/>.
/// </summary>
/// <remarks>
/// Preferences are stored per-app in the platform's native preferences store:
/// <list type="bullet">
///   <item><description>iOS — NSUserDefaults.</description></item>
///   <item><description>Android — SharedPreferences.</description></item>
/// </list>
/// </remarks>
internal sealed class MauiOpencodeSettingsService : IOpencodeSettingsService
{
    private const string TimeoutKey = "opencode_timeout_seconds";
    private const int DefaultTimeoutSeconds = 120;

    /// <inheritdoc />
    public int GetTimeoutSeconds()
        => Preferences.Default.Get(TimeoutKey, DefaultTimeoutSeconds);

    /// <inheritdoc />
    public Task SetTimeoutSecondsAsync(int value, CancellationToken ct = default)
    {
        Preferences.Default.Set(TimeoutKey, value);
        return Task.CompletedTask;
    }
}
