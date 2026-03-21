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
    private readonly IAppStateService _appStateService;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    private ProjectDto? _activeProject;
    private bool _initialized;

    /// <summary>Initialises the ActiveProjectService with the required dependencies.</summary>
    /// <param name="projectService">Service for project operations (used to fetch project data from the server).</param>
    /// <param name="appStateService">Service for persisting global app state (last active project ID).</param>
    public ActiveProjectService(IProjectService projectService, IAppStateService appStateService)
    {
        ArgumentNullException.ThrowIfNull(projectService);
        ArgumentNullException.ThrowIfNull(appStateService);
        _projectService = projectService;
        _appStateService = appStateService;
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

        await _appStateService.SetLastActiveProjectIdAsync(projectId, ct).ConfigureAwait(false);

        WeakReferenceMessenger.Default.Send(new ActiveProjectChangedMessage(project));

        return true;
    }

    /// <inheritdoc />
    public string? GetCachedWorktree() => _activeProject?.Worktree;
}
