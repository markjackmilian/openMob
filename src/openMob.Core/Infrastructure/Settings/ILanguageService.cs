namespace openMob.Core.Infrastructure.Settings;

/// <summary>
/// Provides persistence for the user's preferred app language.
/// </summary>
public interface ILanguageService
{
    /// <summary>
    /// Gets the saved language code, or <c>en</c> when no preference has been stored.
    /// </summary>
    /// <returns>The saved language code.</returns>
    string GetLanguageCode();

    /// <summary>
    /// Persists the given language code.
    /// </summary>
    /// <param name="languageCode">The BCP-47 language code to store.</param>
    /// <param name="ct">Cancellation token.</param>
    Task SetLanguageCodeAsync(string languageCode, CancellationToken ct = default);
}
