using openMob.Core.Infrastructure.Http;
using openMob.Core.Infrastructure.Http.Dtos.Opencode.Requests;

namespace openMob.Core.Services;

/// <summary>
/// Implementation of <see cref="IFileService"/> that wraps
/// <see cref="IOpencodeApiClient.FindFilesAsync"/> to return a flat list of project files.
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
        var result = await _apiClient.FindFilesAsync(new FindFilesRequest(Pattern: "**", Path: null), ct)
            .ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return OpencodeResult<IReadOnlyList<FileDto>>.Failure(result.Error!);
        }

        var paths = result.Value!;
        var files = new List<FileDto>(paths.Count);

        foreach (var path in paths)
        {
            var name = ExtractFileName(path);
            files.Add(new FileDto(RelativePath: path, Name: name));
        }

        return OpencodeResult<IReadOnlyList<FileDto>>.Success(files.AsReadOnly());
    }

    /// <summary>Extracts the file name from a relative path.</summary>
    private static string ExtractFileName(string relativePath)
    {
        var lastSlash = relativePath.LastIndexOf('/');
        return lastSlash >= 0 ? relativePath[(lastSlash + 1)..] : relativePath;
    }
}
