using openMob.Core.Infrastructure.Http;
using openMob.Core.Infrastructure.Http.Dtos.Opencode;
using openMob.Core.Infrastructure.Monitoring;

namespace openMob.Core.Services;

/// <summary>
/// Implementation of <see cref="IProjectService"/> that wraps <see cref="IOpencodeApiClient"/>
/// project methods with error handling and result unwrapping.
/// </summary>
internal sealed class ProjectService : IProjectService
{
    private readonly IOpencodeApiClient _apiClient;

    /// <summary>Initialises the service with the opencode API client.</summary>
    /// <param name="apiClient">The opencode API client.</param>
    public ProjectService(IOpencodeApiClient apiClient)
    {
        ArgumentNullException.ThrowIfNull(apiClient);
        _apiClient = apiClient;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProjectDto>> GetAllProjectsAsync(CancellationToken ct = default)
    {
        var result = await _apiClient.GetProjectsAsync(ct).ConfigureAwait(false);

        if (result.IsSuccess && result.Value is not null)
            return result.Value;

        if (result.Error is not null)
        {
            SentryHelper.CaptureException(
                new InvalidOperationException($"Failed to get projects: {result.Error.Message}"),
                new Dictionary<string, object> { ["errorKind"] = result.Error.Kind.ToString() });
        }

        return [];
    }

    /// <inheritdoc />
    public async Task<ProjectDto?> GetCurrentProjectAsync(CancellationToken ct = default)
    {
        var result = await _apiClient.GetCurrentProjectAsync(ct).ConfigureAwait(false);

        if (result.IsSuccess)
            return result.Value;

        if (result.Error is not null && result.Error.Kind != ErrorKind.NotFound)
        {
            SentryHelper.CaptureException(
                new InvalidOperationException($"Failed to get current project: {result.Error.Message}"),
                new Dictionary<string, object> { ["errorKind"] = result.Error.Kind.ToString() });
        }

        return null;
    }

    /// <inheritdoc />
    public async Task<ProjectDto?> GetProjectByIdAsync(string id, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        var projects = await GetAllProjectsAsync(ct).ConfigureAwait(false);
        return projects.FirstOrDefault(p => p.Id == id);
    }

    /// <inheritdoc />
    public async Task<ProjectDto?> GetProjectByWorktreeAsync(string worktree, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(worktree);

        var projects = await GetAllProjectsAsync(ct).ConfigureAwait(false);
        return projects.FirstOrDefault(p => string.Equals(p.Worktree, worktree, StringComparison.Ordinal));
    }

    /// <inheritdoc />
    public async Task<ProjectDto?> EnsureProjectForWorktreeAsync(string worktree, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(worktree);

        var existing = await GetProjectByWorktreeAsync(worktree, ct).ConfigureAwait(false);
        if (existing is not null)
            return existing;

        var sessionResult = await _apiClient.CreateSessionForDirectoryAsync(worktree, ct).ConfigureAwait(false);
        if (!sessionResult.IsSuccess || sessionResult.Value is null)
        {
            if (sessionResult.Error is not null)
            {
                SentryHelper.CaptureException(
                    new InvalidOperationException($"Failed to register project for '{worktree}': {sessionResult.Error.Message}"),
                    new Dictionary<string, object>
                    {
                        ["worktree"] = worktree,
                        ["errorKind"] = sessionResult.Error.Kind.ToString(),
                    });
            }

            return null;
        }

        var project = await GetProjectByIdAsync(sessionResult.Value.ProjectId, ct).ConfigureAwait(false);
        if (project is not null)
            return project;

        return await GetProjectByWorktreeAsync(worktree, ct).ConfigureAwait(false);
    }
}
