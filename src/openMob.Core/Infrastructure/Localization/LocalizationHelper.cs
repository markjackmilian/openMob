using System.Globalization;

namespace openMob.Core.Infrastructure.Localization;

/// <summary>
/// Applies the app culture for the selected language code.
/// </summary>
public static class LocalizationHelper
{
    /// <summary>
    /// Applies the specified language code to the current and default thread cultures.
    /// </summary>
    /// <param name="languageCode">The BCP-47 language code, such as <c>en</c> or <c>it</c>.</param>
    public static void ApplyCulture(string? languageCode)
    {
        var culture = ResolveCulture(languageCode);

        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
    }

    /// <summary>
    /// Resolves the culture for the provided language code.
    /// </summary>
    /// <param name="languageCode">The language code to resolve.</param>
    /// <returns>The resolved culture.</returns>
    public static CultureInfo ResolveCulture(string? languageCode)
    {
        if (string.Equals(languageCode, "it", StringComparison.OrdinalIgnoreCase))
            return CultureInfo.GetCultureInfo("it");

        return CultureInfo.GetCultureInfo("en");
    }
}
