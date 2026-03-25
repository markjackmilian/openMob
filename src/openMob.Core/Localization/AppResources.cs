using System.Globalization;
using System.Resources;

namespace openMob.Core.Localization;

/// <summary>
/// Provides localized strings for the app.
/// </summary>
public static class AppResources
{
    private static readonly ResourceManager ResourceManager = new(
        "openMob.Core.Resources.AppResources",
        typeof(AppResources).Assembly);

    /// <summary>
    /// Gets a localized string for the specified resource key.
    /// </summary>
    /// <param name="key">The resource key.</param>
    /// <returns>The localized string, or the key if no value exists.</returns>
    public static string Get(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        return ResourceManager.GetString(key, CultureInfo.CurrentUICulture)
            ?? ResourceManager.GetString(key, CultureInfo.GetCultureInfo("en"))
            ?? key;
    }

    /// <summary>
    /// Gets a formatted localized string for the specified resource key.
    /// </summary>
    /// <param name="key">The resource key.</param>
    /// <param name="args">Format arguments.</param>
    /// <returns>The formatted localized string.</returns>
    public static string GetFormatted(string key, params object[] args)
        => string.Format(CultureInfo.CurrentUICulture, Get(key), args);
}
