namespace openMob.Core.Infrastructure.Settings;

/// <summary>
/// Represents the user's preferred application colour theme.
/// </summary>
/// <remarks>
/// <para>
/// This enum is persisted as an <see langword="int"/> via <c>Preferences.Default</c> in the MAUI project.
/// The default value (<c>0</c>) is <see cref="System"/>, which means the app follows the OS theme
/// when no explicit preference has been stored.
/// </para>
/// <para>
/// Mapping to <c>Microsoft.Maui.ApplicationModel.AppTheme</c> is performed in
/// <c>MauiThemeService</c> (MAUI project) to keep this enum free of MAUI dependencies.
/// </para>
/// </remarks>
public enum AppThemePreference
{
    /// <summary>
    /// Follow the operating system's current theme setting (light or dark).
    /// This is the default value when no preference has been explicitly stored.
    /// </summary>
    System = 0,

    /// <summary>
    /// Always display the application in light mode, regardless of the OS setting.
    /// </summary>
    Light = 1,

    /// <summary>
    /// Always display the application in dark mode, regardless of the OS setting.
    /// </summary>
    Dark = 2,
}
