using openMob.Core.Infrastructure.Http;

namespace openMob.Core.Services;

/// <summary>
/// Service for listing project files from the opencode server.
/// Wraps <see cref="IOpencodeApiClient"/> file search and returns a flat list.
/// </summary>
public interface IFileService
{
    /// <summary>
    /// Gets a flat list of project files.
    /// </summary>
    Task<OpencodeResult<IReadOnlyList<FileDto>>> GetFilesAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets the first-level file tree nodes at the specified path.
    /// </summary>
    /// <param name="path">The directory path relative to project root, or <c>null</c> for root.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A flat list of file and directory nodes at the specified level.</returns>
    Task<OpencodeResult<IReadOnlyList<FileDto>>> GetFileTreeAsync(string? path = null, CancellationToken ct = default);

    /// <summary>
    /// Searches for files matching the specified glob pattern via the server.
    /// </summary>
    /// <param name="pattern">The glob pattern (e.g. <c>*foo*</c>).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A flat list of matching files.</returns>
    Task<OpencodeResult<IReadOnlyList<FileDto>>> FindFilesAsync(string pattern, CancellationToken ct = default);
}
