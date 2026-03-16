namespace openMob.Core.Infrastructure.Settings;

/// <summary>
/// Provides theme preference persistence and application for the openMob app.
/// </summary>
/// <remarks>
/// <para>
/// This interface lives in <c>openMob.Core</c> with zero MAUI dependencies — all parameter
/// and return types are BCL types or types defined within <c>openMob.Core</c>.
/// </para>
/// <para>
/// The concrete implementation (<c>MauiThemeService</c>) resides in the MAUI project and uses
/// <c>Preferences.Default</c> for persistence and <c>Application.Current.UserAppTheme</c>
/// for immediate application. It is registered as a Singleton in <c>MauiProgram.cs</c>.
/// </para>
/// </remarks>
public interface IThemeService
{
    /// <summary>
    /// Returns the currently persisted theme preference.
    /// </summary>
    /// <returns>
    /// The <see cref="AppThemePreference"/> stored by the user, or
    /// <see cref="AppThemePreference.System"/> if no preference has been saved yet.
    /// </returns>
    AppThemePreference GetTheme();

    /// <summary>
    /// Persists the given theme preference and applies it immediately to the running application.
    /// </summary>
    /// <param name="preference">The theme preference to store and apply.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="Task"/> that completes once the preference is persisted and the theme applied.</returns>
    Task SetThemeAsync(AppThemePreference preference, CancellationToken ct = default);
}
