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
}
