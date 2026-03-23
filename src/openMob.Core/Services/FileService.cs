using openMob.Core.Infrastructure.Http;
using openMob.Core.Infrastructure.Http.Dtos.Opencode;
using openMob.Core.Infrastructure.Http.Dtos.Opencode.Requests;

namespace openMob.Core.Services;

/// <summary>
/// Implementation of <see cref="IFileService"/> that wraps
/// <see cref="IOpencodeApiClient"/> file endpoints to return project files.
/// </summary>
/// <remarks>
/// <para>
/// The opencode server <c>GET /file</c> endpoint does NOT support recursive glob patterns.
/// When <c>path</c> is empty the server ignores the pattern entirely and returns all root
/// entries. Therefore <see cref="FindFilesAsync"/> performs a client-side BFS traversal
/// up to <see cref="MaxSearchDepth"/> levels deep, then filters by name.
/// </para>
/// </remarks>
internal sealed class FileService : IFileService
{
    /// <summary>
    /// Maximum directory depth explored during BFS file search.
    /// Limits the number of API calls for very large project trees.
    /// </summary>
    private const int MaxSearchDepth = 4;

    private readonly IOpencodeApiClient _apiClient;

    /// <summary>Initialises the file service with the API client.</summary>
    /// <param name="apiClient">The opencode API client.</param>
    public FileService(IOpencodeApiClient apiClient)
    {
        ArgumentNullException.ThrowIfNull(apiClient);
        _apiClient = apiClient;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Retrieves the root-level file tree and returns all entries as a flat list.
    /// Uses <see cref="IOpencodeApiClient.GetFileTreeAsync"/> with <c>path=null</c>
    /// (project root) rather than the broken <c>FindFilesAsync</c> with an empty path.
    /// </remarks>
    public async Task<OpencodeResult<IReadOnlyList<FileDto>>> GetFilesAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var result = await _apiClient.GetFileTreeAsync(null, ct).ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return OpencodeResult<IReadOnlyList<FileDto>>.Failure(result.Error!);
        }

        var nodes = result.Value!;
        var files = new List<FileDto>(nodes.Count);

        foreach (var node in nodes)
        {
            files.Add(MapNodeToFileDto(node));
        }

        return OpencodeResult<IReadOnlyList<FileDto>>.Success(files.AsReadOnly());
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
            files.Add(MapNodeToFileDto(node));
        }

        return OpencodeResult<IReadOnlyList<FileDto>>.Success(files.AsReadOnly());
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// Because the opencode server ignores the glob pattern when <c>path</c> is empty,
    /// this method performs a client-side BFS traversal of the project tree up to
    /// <see cref="MaxSearchDepth"/> levels deep, then filters the collected nodes by
    /// whether their <c>Name</c> contains the search term extracted from the pattern.
    /// </para>
    /// <para>
    /// Pattern extraction: <c>*foo*</c> → search term <c>foo</c>. Leading and trailing
    /// <c>*</c> wildcards are stripped. An empty or wildcard-only pattern returns all nodes.
    /// </para>
    /// <para>
    /// A node matches if the search term is found in its <c>Name</c> (last path segment)
    /// <em>or</em> in its full <c>RelativePath</c>. This means searching for
    /// <c>in-progress</c> returns both the <c>in-progress</c> directory and every file
    /// whose path contains that segment (e.g. <c>specs/in-progress/foo.md</c>).
    /// </para>
    /// <para>
    /// Nodes with <c>Ignored = true</c> are skipped during traversal and excluded from results.
    /// </para>
    /// </remarks>
    public async Task<OpencodeResult<IReadOnlyList<FileDto>>> FindFilesAsync(
        string pattern,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        // Extract the search term from the glob pattern (strip leading/trailing '*').
        var searchTerm = pattern.Trim('*');

        // BFS queue: (directoryPath, currentDepth).
        // null path = project root (GetFileTreeAsync normalises null → empty string internally).
        var queue = new Queue<(string? Path, int Depth)>();
        queue.Enqueue((null, 0));

        var matches = new List<FileDto>();

        while (queue.Count > 0)
        {
            ct.ThrowIfCancellationRequested();

            var (dirPath, depth) = queue.Dequeue();

            var result = await _apiClient.GetFileTreeAsync(dirPath, ct).ConfigureAwait(false);

            if (!result.IsSuccess)
            {
                // Propagate the first API error encountered.
                return OpencodeResult<IReadOnlyList<FileDto>>.Failure(result.Error!);
            }

            foreach (var node in result.Value!)
            {
                // Skip VCS-ignored entries entirely.
                if (node.Ignored)
                    continue;

                // Collect nodes whose name OR relative path contains the search term.
                // Matching on Path allows queries like "in-progress" to surface both the
                // directory itself and all files nested under it (e.g. specs/in-progress/foo.md).
                if (string.IsNullOrEmpty(searchTerm) ||
                    node.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                    node.Path.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                {
                    matches.Add(MapNodeToFileDto(node));
                }

                // Enqueue subdirectories for further traversal if within depth limit.
                if (node.Type == "directory" && depth < MaxSearchDepth)
                {
                    queue.Enqueue((node.Path, depth + 1));
                }
            }
        }

        return OpencodeResult<IReadOnlyList<FileDto>>.Success(matches.AsReadOnly());
    }

    /// <summary>Maps a <see cref="FileNodeDto"/> to a <see cref="FileDto"/>.</summary>
    /// <param name="node">The file node returned by the API.</param>
    /// <returns>A <see cref="FileDto"/> with <c>RelativePath</c>, <c>Name</c>, and <c>Type</c> populated.</returns>
    private static FileDto MapNodeToFileDto(FileNodeDto node)
        => new(RelativePath: node.Path, Name: node.Name, Type: node.Type);
}
