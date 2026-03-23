using openMob.Core.Infrastructure.Http;
using openMob.Core.Infrastructure.Http.Dtos.Opencode.Requests;

namespace openMob.Core.Services;

/// <summary>
/// Implementation of <see cref="IFileService"/> that wraps
/// <see cref="IOpencodeApiClient"/> file endpoints to return project files.
/// </summary>
internal sealed class FileService : IFileService
{
    private readonly IOpencodeApiClient _apiClient;

    /// <summary>Initialises the file service with the API client.</summary>
    /// <param name="apiClient">The opencode API client.</param>
    public FileService(IOpencodeApiClient apiClient)
    {
        ArgumentNullException.ThrowIfNull(apiClient);
        _apiClient = apiClient;
    }

    /// <inheritdoc />
    public async Task<OpencodeResult<IReadOnlyList<FileDto>>> GetFilesAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        // Use FindFilesAsync with a wildcard pattern to get a flat list of all file paths.
        // This avoids the need to recursively traverse the file tree from GetFileTreeAsync.
        var result = await _apiClient.FindFilesAsync(new FindFilesRequest(Pattern: "**", Path: ""), ct)
            .ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return OpencodeResult<IReadOnlyList<FileDto>>.Failure(result.Error!);
        }

        return OpencodeResult<IReadOnlyList<FileDto>>.Success(MapPathsToFileDtos(result.Value!));
    }

    /// <inheritdoc />
    public async Task<OpencodeResult<IReadOnlyList<FileDto>>> GetFileTreeAsync(
        string? path = null,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        // Pass path as-is (null for root). OpencodeApiClient.GetFileTreeAsync normalises null
        // to an empty string when building the query parameter, so /file?pattern=*&path= is used
        // for the project root — matching the verified curl behaviour.
        var result = await _apiClient.GetFileTreeAsync(path, ct).ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return OpencodeResult<IReadOnlyList<FileDto>>.Failure(result.Error!);
        }

        var nodes = result.Value!;
        var files = new List<FileDto>(nodes.Count);

        foreach (var node in nodes)
        {
            // Map FileNodeDto.Path → FileDto.RelativePath and propagate Type.
            files.Add(new FileDto(RelativePath: node.Path, Name: node.Name, Type: node.Type));
        }

        return OpencodeResult<IReadOnlyList<FileDto>>.Success(files.AsReadOnly());
    }

    /// <inheritdoc />
    public async Task<OpencodeResult<IReadOnlyList<FileDto>>> FindFilesAsync(
        string pattern,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var result = await _apiClient.FindFilesAsync(new FindFilesRequest(Pattern: pattern, Path: ""), ct)
            .ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return OpencodeResult<IReadOnlyList<FileDto>>.Failure(result.Error!);
        }

        return OpencodeResult<IReadOnlyList<FileDto>>.Success(MapPathsToFileDtos(result.Value!));
    }

    /// <summary>Maps a list of relative path strings to <see cref="FileDto"/> instances.</summary>
    private static IReadOnlyList<FileDto> MapPathsToFileDtos(IReadOnlyList<string> paths)
    {
        var files = new List<FileDto>(paths.Count);

        foreach (var path in paths)
        {
            var name = ExtractFileName(path);
            files.Add(new FileDto(RelativePath: path, Name: name));
        }

        return files.AsReadOnly();
    }

    /// <summary>Extracts the file name from a relative path.</summary>
    private static string ExtractFileName(string relativePath)
    {
        var lastSlash = relativePath.LastIndexOf('/');
        return lastSlash >= 0 ? relativePath[(lastSlash + 1)..] : relativePath;
    }
}
