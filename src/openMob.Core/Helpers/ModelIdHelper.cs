namespace openMob.Core.Helpers;

/// <summary>
/// Helper methods for parsing model identifiers in "providerId/modelId" format.
/// </summary>
internal static class ModelIdHelper
{
    /// <summary>
    /// Extracts the display model name from a "providerId/modelId" format string.
    /// Returns the part after the first '/'.
    /// If no '/' is present, returns the full string.
    /// </summary>
    /// <param name="fullModelId">The full model identifier (e.g., "anthropic/claude-sonnet-4-5").</param>
    /// <returns>The model display name (e.g., "claude-sonnet-4-5").</returns>
    internal static string ExtractModelName(string fullModelId)
    {
        var slashIndex = fullModelId.IndexOf('/');
        return slashIndex >= 0 ? fullModelId[(slashIndex + 1)..] : fullModelId;
    }
}
