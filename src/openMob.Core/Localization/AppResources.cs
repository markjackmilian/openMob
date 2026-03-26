using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Resources;

namespace openMob.Core.Localization;

/// <summary>
/// Provides localized strings for the app.
/// </summary>
public static class AppResources
{
    // [DynamicDependency] tells the iOS linker that the embedded .resources
    // manifest accessed by name string must never be stripped. Without this,
    // the Release linker removes the resource infrastructure and ResourceManager
    // throws during the static initializer (.cctor), causing load_aot_module to
    // fail with an abort() before the app even starts.
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, "openMob.Core.Resources.AppResources", "openMob.Core")]
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
