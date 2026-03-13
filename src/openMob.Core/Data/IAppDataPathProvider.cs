namespace openMob.Core.Data;

/// <summary>
/// Provides the platform-specific application data directory path.
/// Abstracts <c>FileSystem.AppDataDirectory</c> (MAUI API) to keep Core free of MAUI dependencies.
/// </summary>
public interface IAppDataPathProvider
{
    /// <summary>Gets the full path to the application data directory.</summary>
    string AppDataPath { get; }
}
