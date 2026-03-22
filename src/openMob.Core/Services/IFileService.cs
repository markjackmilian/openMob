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
}
