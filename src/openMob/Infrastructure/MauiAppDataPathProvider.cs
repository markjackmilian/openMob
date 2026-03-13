using openMob.Core.Data;

namespace openMob.Infrastructure;

/// <summary>
/// MAUI implementation of <see cref="IAppDataPathProvider"/>.
/// Returns the platform-specific application data directory via <see cref="FileSystem.AppDataDirectory"/>.
/// </summary>
/// <remarks>
/// Platform behaviour:
/// - iOS: maps to the app's Library directory (backed up by iCloud by default).
/// - Android: maps to the app's internal files directory (not accessible to other apps).
/// </remarks>
internal sealed class MauiAppDataPathProvider : IAppDataPathProvider
{
    /// <inheritdoc />
    public string AppDataPath => FileSystem.AppDataDirectory;
}
