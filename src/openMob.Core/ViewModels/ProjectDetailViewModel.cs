using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using openMob.Core.Helpers;
using openMob.Core.Infrastructure.Logging;
using openMob.Core.Infrastructure.Monitoring;
using openMob.Core.Models;
using openMob.Core.Services;

namespace openMob.Core.ViewModels;

/// <summary>
/// ViewModel for the ProjectDetailPage. Displays project information, recent sessions,
/// and provides actions for setting active, creating sessions, and configuring
/// agent/model defaults (REQ-024, REQ-025, REQ-026).
/// </summary>
public sealed partial class ProjectDetailViewModel : ObservableObject
{
    private readonly IProjectService _projectService;
    private readonly ISessionService _sessionService;
    private readonly INavigationService _navigationService;
    private readonly IAppPopupService _popupService;
    private readonly IProjectPreferenceService _preferenceService;
    private readonly IActiveProjectService _activeProjectService;

    /// <summary>Initialises the ProjectDetailViewModel with required dependencies.</summary>
    /// <param name="projectService">Service for project operations.</param>
    /// <param name="sessionService">Service for session operations.</param>
    /// <param name="navigationService">Service for Shell navigation.</param>
    /// <param name="popupService">Service for popup/dialog operations.</param>
    /// <param name="preferenceService">Service for per-project preference persistence.</param>
    /// <param name="activeProjectService">Service for managing the client-side active project state.</param>
    public ProjectDetailViewModel(
        IProjectService projectService,
        ISessionService sessionService,
        INavigationService navigationService,
        IAppPopupService popupService,
        IProjectPreferenceService preferenceService,
        IActiveProjectService activeProjectService)
    {
        ArgumentNullException.ThrowIfNull(projectService);
        ArgumentNullException.ThrowIfNull(sessionService);
        ArgumentNullException.ThrowIfNull(navigationService);
        ArgumentNullException.ThrowIfNull(popupService);
        ArgumentNullException.ThrowIfNull(preferenceService);
        ArgumentNullException.ThrowIfNull(activeProjectService);

        _projectService = projectService;
        _sessionService = sessionService;
        _navigationService = navigationService;
        _popupService = popupService;
        _preferenceService = preferenceService;
        _activeProjectService = activeProjectService;
    }

    // ─── Properties ───────────────────────────────────────────────────────────

    /// <summary>Gets or sets the project identifier.</summary>
    [ObservableProperty]
    private string _projectId = string.Empty;

    /// <summary>Gets or sets the project display name.</summary>
    [ObservableProperty]
    private string _projectName = string.Empty;

    /// <summary>Gets or sets the project worktree path.</summary>
    [ObservableProperty]
    private string _projectPath = string.Empty;

    /// <summary>Gets or sets the project description (derived from VCS info or path).</summary>
    [ObservableProperty]
    private string _projectDescription = string.Empty;

    /// <summary>Gets or sets whether this is the currently active project.</summary>
    [ObservableProperty]
    private bool _isActiveProject;

    /// <summary>Gets or sets the most recent sessions for this project (max 5).</summary>
    [ObservableProperty]
    private ObservableCollection<SessionItem> _recentSessions = [];

    /// <summary>Gets or sets the default agent name for this project.</summary>
    [ObservableProperty]
    private string _defaultAgentName = string.Empty;

    /// <summary>Gets or sets the default model name for this project.</summary>
    [ObservableProperty]
    private string _defaultModelName = string.Empty;

    /// <summary>Gets or sets whether the project data is currently loading.</summary>
    [ObservableProperty]
    private bool _isLoading;

    // ─── Commands ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Loads the project details and recent sessions for the specified project ID.
    /// </summary>
    /// <param name="id">The project identifier to load.</param>
    /// <param name="ct">Cancellation token.</param>
    [RelayCommand]
    private async Task LoadProjectAsync(string id, CancellationToken ct)
    {
#if DEBUG
        var sw = Stopwatch.StartNew();
        DebugLogger.LogCommand(nameof(LoadProjectAsync), "start");
        try
        {
#endif
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        if (IsLoading)
            return;

        IsLoading = true;

        try
        {
            ProjectId = id;

            var project = await _projectService.GetProjectByIdAsync(id, ct);
            if (project is null)
            {
                await _popupService.ShowErrorAsync("Project Not Found", "The requested project could not be found.", ct);
                await _navigationService.PopAsync(ct);
                return;
            }

            var currentProject = await _activeProjectService.GetActiveProjectAsync(ct);

            ProjectName = ProjectNameHelper.ExtractFromWorktree(project.Worktree);
            ProjectPath = project.Worktree;
            ProjectDescription = project.Vcs is not null ? $"VCS: {project.Vcs}" : string.Empty;
            IsActiveProject = currentProject?.Id == project.Id;

            // Load recent sessions (max 5)
            var sessions = await _sessionService.GetSessionsByProjectAsync(id, ct);
            var recentItems = sessions
                .Take(5)
                .Select(s => new SessionItem(
                    Id: s.Id,
                    Title: s.Title,
                    ProjectId: s.ProjectId,
                    UpdatedAt: DateTimeOffset.FromUnixTimeMilliseconds(s.Time.Updated),
                    IsSelected: false))
                .ToList();

            RecentSessions = new ObservableCollection<SessionItem>(recentItems);

            // Load default model preference (REQ-007)
            var pref = await _preferenceService.GetAsync(id, ct).ConfigureAwait(false);
            if (pref?.DefaultModelId is not null)
            {
                DefaultModelName = ModelIdHelper.ExtractModelName(pref.DefaultModelId);
            }
        }
        catch (Exception ex)
        {
            SentryHelper.CaptureException(ex, new Dictionary<string, object>
            {
                ["context"] = "ProjectDetailViewModel.LoadProjectAsync",
                ["projectId"] = id,
            });
        }
        finally
        {
            IsLoading = false;
        }
#if DEBUG
        sw.Stop();
        DebugLogger.LogCommand(nameof(LoadProjectAsync), "complete", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            DebugLogger.LogCommand(nameof(LoadProjectAsync), "failed", error: $"{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            throw;
        }
#endif
    }

    /// <summary>
    /// Sets this project as the active project via <see cref="IActiveProjectService"/> (REQ-024).
    /// On success, updates <see cref="IsActiveProject"/> and shows a confirmation toast.
    /// On failure (project not found), shows an error toast.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    [RelayCommand]
    private async Task SetActiveAsync(CancellationToken ct)
    {
#if DEBUG
        var sw = Stopwatch.StartNew();
        DebugLogger.LogCommand(nameof(SetActiveAsync), "start");
        try
        {
#endif
        var success = await _activeProjectService.SetActiveProjectAsync(ProjectId, ct).ConfigureAwait(false);

        if (success)
        {
            IsActiveProject = true;
            await _popupService.ShowToastAsync($"'{ProjectName}' set as active project.", ct);
        }
        else
        {
            await _popupService.ShowErrorAsync("Error", "Failed to set the active project. Project not found.", ct);
        }
#if DEBUG
        sw.Stop();
        DebugLogger.LogCommand(nameof(SetActiveAsync), "complete", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            DebugLogger.LogCommand(nameof(SetActiveAsync), "failed", error: $"{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            throw;
        }
#endif
    }

    /// <summary>
    /// Creates a new session in this project and navigates to the ChatPage (REQ-025).
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    [RelayCommand]
    private async Task NewSessionAsync(CancellationToken ct)
    {
#if DEBUG
        var sw = Stopwatch.StartNew();
        DebugLogger.LogCommand(nameof(NewSessionAsync), "start");
        try
        {
#endif
        var session = await _sessionService.CreateSessionAsync(null, ct);

        if (session is not null)
        {
            await _navigationService.GoToAsync("//chat", new Dictionary<string, object>
            {
                ["sessionId"] = session.Id,
            }, ct);
        }
        else
        {
            await _popupService.ShowErrorAsync("Error", "Failed to create a new session.", ct);
        }
#if DEBUG
        sw.Stop();
        DebugLogger.LogCommand(nameof(NewSessionAsync), "complete", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            DebugLogger.LogCommand(nameof(NewSessionAsync), "failed", error: $"{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            throw;
        }
#endif
    }

    /// <summary>
    /// Shows the AgentPickerSheet for changing the default agent (REQ-026).
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    [RelayCommand]
    private async Task ChangeAgentAsync(CancellationToken ct)
    {
#if DEBUG
        var sw = Stopwatch.StartNew();
        DebugLogger.LogCommand(nameof(ChangeAgentAsync), "start");
        try
        {
#endif
        // The View layer handles creating and pushing the AgentPickerSheet popup.
        // This command signals the intent.
        await Task.CompletedTask;
#if DEBUG
        sw.Stop();
        DebugLogger.LogCommand(nameof(ChangeAgentAsync), "complete", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            DebugLogger.LogCommand(nameof(ChangeAgentAsync), "failed", error: $"{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            throw;
        }
#endif
    }

    /// <summary>
    /// Opens the ModelPickerSheet, receives the selected model via callback,
    /// updates <see cref="DefaultModelName"/>, and persists the choice to SQLite (REQ-007, REQ-026).
    /// If persistence fails, the UI is reverted to the previous model name and an error toast is shown.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    [RelayCommand]
    private async Task ChangeModelAsync(CancellationToken ct)
    {
#if DEBUG
        var sw = Stopwatch.StartNew();
        DebugLogger.LogCommand(nameof(ChangeModelAsync), "start");
        try
        {
#endif
        string? selectedModelId = null;
        var previousModelName = DefaultModelName;

        await _popupService.ShowModelPickerAsync(modelId =>
        {
            selectedModelId = modelId;
            DefaultModelName = ModelIdHelper.ExtractModelName(modelId);
        }, ct).ConfigureAwait(false);

        if (selectedModelId is null)
            return;

        var persisted = await _preferenceService
            .SetDefaultModelAsync(ProjectId, selectedModelId, ct)
            .ConfigureAwait(false);

        if (!persisted)
        {
            DefaultModelName = previousModelName;
            await _popupService.ShowToastAsync(
                "Failed to save model preference. Please try again.", ct).ConfigureAwait(false);
        }
#if DEBUG
        sw.Stop();
        DebugLogger.LogCommand(nameof(ChangeModelAsync), "complete", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            DebugLogger.LogCommand(nameof(ChangeModelAsync), "failed", error: $"{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            throw;
        }
#endif
    }

    /// <summary>
    /// Shows a confirmation dialog and deletes the project if confirmed.
    /// Navigates back to the projects list after deletion.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    [RelayCommand]
    private async Task DeleteProjectAsync(CancellationToken ct)
    {
#if DEBUG
        var sw = Stopwatch.StartNew();
        DebugLogger.LogCommand(nameof(DeleteProjectAsync), "start");
        try
        {
#endif
        var confirmed = await _popupService.ShowConfirmDeleteAsync(
            "Delete Project",
            $"Are you sure you want to delete '{ProjectName}'? This action cannot be undone.",
            ct);

        if (!confirmed)
            return;

        // Note: opencode server manages projects by directory. Deletion is not
        // directly supported via the API in the current version.
        await _popupService.ShowToastAsync("Project deletion is managed by the server.", ct);
        await _navigationService.PopAsync(ct);
#if DEBUG
        sw.Stop();
        DebugLogger.LogCommand(nameof(DeleteProjectAsync), "complete", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            DebugLogger.LogCommand(nameof(DeleteProjectAsync), "failed", error: $"{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            throw;
        }
#endif
    }

}
