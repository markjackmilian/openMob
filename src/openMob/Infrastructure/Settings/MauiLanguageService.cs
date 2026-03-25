using Microsoft.Maui.Storage;
using openMob.Core.Infrastructure.Settings;

namespace openMob.Infrastructure.Settings;

/// <summary>
/// MAUI-backed implementation of <see cref="ILanguageService"/> using <see cref="Preferences"/>.
/// </summary>
internal sealed class MauiLanguageService : ILanguageService
{
    private const string LanguageKey = "app_language_code";

    /// <inheritdoc />
    public string GetLanguageCode()
        => Preferences.Default.Get(LanguageKey, "en");

    /// <inheritdoc />
    public Task SetLanguageCodeAsync(string languageCode, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(languageCode);

        var normalized = string.Equals(languageCode, "it", StringComparison.OrdinalIgnoreCase)
            ? "it"
            : "en";

        Preferences.Default.Set(LanguageKey, normalized);
        return Task.CompletedTask;
    }
}
