using CommunityToolkit.Mvvm.Messaging;
using openMob.Core.Infrastructure.Http.Dtos.Opencode;
using openMob.Core.Messages;

namespace openMob.Core.Services;

/// <summary>
/// Manages the client-side active project state. On first access, initialises from the
/// server's current project via <see cref="IProjectService"/>. When the user switches
/// projects, caches the override in memory and publishes an
/// <see cref="ActiveProjectChangedMessage"/> via <see cref="WeakReferenceMessenger"/>.
/// </summary>
/// <remarks>
/// Registered as Singleton — the active project state is global and persists for the app lifetime.
/// Thread-safe initialisation is guaranteed by a <see cref="SemaphoreSlim"/>.
/// </remarks>
internal sealed class ActiveProjectService : IActiveProjectService
{
    private readonly IProjectService _projectService;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    private ProjectDto? _activeProject;
    private bool _initialized;

    /// <summary>Initialises the ActiveProjectService with the required project service.</summary>
    /// <param name="projectService">Service for project operations (used to fetch project data from the server).</param>
    public ActiveProjectService(IProjectService projectService)
    {
        ArgumentNullException.ThrowIfNull(projectService);
        _projectService = projectService;
    }

    /// <inheritdoc />
    public async Task<ProjectDto?> GetActiveProjectAsync(CancellationToken ct = default)
    {
        if (_initialized)
            return _activeProject;

        await _initLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_initialized)
                return _activeProject;

            _activeProject = await _projectService.GetCurrentProjectAsync(ct).ConfigureAwait(false);
            _initialized = true;

            return _activeProject;
        }
        finally
        {
            _initLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<bool> SetActiveProjectAsync(string projectId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectId);

        var project = await _projectService.GetProjectByIdAsync(projectId, ct).ConfigureAwait(false);

        if (project is null)
            return false;

        _activeProject = project;
        _initialized = true;

        WeakReferenceMessenger.Default.Send(new ActiveProjectChangedMessage(project));

        return true;
    }

    /// <inheritdoc />
    public string? GetCachedWorktree() => _activeProject?.Worktree;
}
