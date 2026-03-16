using Microsoft.Maui.ApplicationModel;
using openMob.Core.Infrastructure.Settings;

namespace openMob.Infrastructure.Settings;

/// <summary>
/// MAUI implementation of <see cref="IThemeService"/> backed by
/// <see cref="Microsoft.Maui.Storage.Preferences"/> for persistence and
/// <see cref="Application.UserAppTheme"/> for immediate application.
/// </summary>
/// <remarks>
/// <para>
/// Preferences are stored per-app in the platform's native preferences store:
/// <list type="bullet">
///   <item><description>iOS — NSUserDefaults.</description></item>
///   <item><description>Android — SharedPreferences.</description></item>
/// </list>
/// </para>
/// <para>
/// <see cref="Application.UserAppTheme"/> must be set on the main thread.
/// <see cref="SetThemeAsync"/> uses <see cref="MainThread.InvokeOnMainThreadAsync"/> to
/// guarantee this, even when called from a background context.
/// </para>
/// </remarks>
internal sealed class MauiThemeService : IThemeService
{
    private const string ThemeKey = "app_theme_preference";

    /// <inheritdoc />
    public AppThemePreference GetTheme()
    {
        var stored = Preferences.Default.Get(ThemeKey, (int)AppThemePreference.System);
        return (AppThemePreference)stored;
    }

    /// <inheritdoc />
    public async Task SetThemeAsync(AppThemePreference preference, CancellationToken ct = default)
    {
        // 1. Persist the preference.
        Preferences.Default.Set(ThemeKey, (int)preference);

        // 2. Map AppThemePreference → Microsoft.Maui.ApplicationModel.AppTheme.
        var mauiTheme = preference switch
        {
            AppThemePreference.Light => AppTheme.Light,
            AppThemePreference.Dark  => AppTheme.Dark,
            _                        => AppTheme.Unspecified,
        };

        // 3. Apply on the main thread — UserAppTheme must be set on the UI thread.
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            Application.Current!.UserAppTheme = mauiTheme;
        }).ConfigureAwait(false);
    }
}
