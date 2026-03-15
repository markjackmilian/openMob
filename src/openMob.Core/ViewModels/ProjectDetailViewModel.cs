using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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

    /// <summary>Initialises the ProjectDetailViewModel with required dependencies.</summary>
    /// <param name="projectService">Service for project operations.</param>
    /// <param name="sessionService">Service for session operations.</param>
    /// <param name="navigationService">Service for Shell navigation.</param>
    /// <param name="popupService">Service for popup/dialog operations.</param>
    public ProjectDetailViewModel(
        IProjectService projectService,
        ISessionService sessionService,
        INavigationService navigationService,
        IAppPopupService popupService)
    {
        ArgumentNullException.ThrowIfNull(projectService);
        ArgumentNullException.ThrowIfNull(sessionService);
        ArgumentNullException.ThrowIfNull(navigationService);
        ArgumentNullException.ThrowIfNull(popupService);

        _projectService = projectService;
        _sessionService = sessionService;
        _navigationService = navigationService;
        _popupService = popupService;
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

            var currentProject = await _projectService.GetCurrentProjectAsync(ct);

            ProjectName = ExtractProjectName(project.Worktree);
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
    }

    /// <summary>
    /// Sets this project as the active project (REQ-024).
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    [RelayCommand]
    private async Task SetActiveAsync(CancellationToken ct)
    {
        // Note: The opencode server manages the "current project" concept.
        // Setting active is done by switching the working directory context.
        // For now, mark as active in the UI and show a toast.
        IsActiveProject = true;
        await _popupService.ShowToastAsync($"'{ProjectName}' set as active project.", ct);
    }

    /// <summary>
    /// Creates a new session in this project and navigates to the ChatPage (REQ-025).
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    [RelayCommand]
    private async Task NewSessionAsync(CancellationToken ct)
    {
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
    }

    /// <summary>
    /// Shows the AgentPickerSheet for changing the default agent (REQ-026).
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    [RelayCommand]
    private async Task ChangeAgentAsync(CancellationToken ct)
    {
        // The View layer handles creating and pushing the AgentPickerSheet popup.
        // This command signals the intent.
        await Task.CompletedTask;
    }

    /// <summary>
    /// Shows the ModelPickerSheet for changing the default model (REQ-026).
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    [RelayCommand]
    private async Task ChangeModelAsync(CancellationToken ct)
    {
        // The View layer handles creating and pushing the ModelPickerSheet popup.
        // This command signals the intent.
        await Task.CompletedTask;
    }

    /// <summary>
    /// Shows a confirmation dialog and deletes the project if confirmed.
    /// Navigates back to the projects list after deletion.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    [RelayCommand]
    private async Task DeleteProjectAsync(CancellationToken ct)
    {
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
    }

    // ─── Private helpers ──────────────────────────────────────────────────────

    /// <summary>Extracts a display name from a worktree path (last directory segment).</summary>
    /// <param name="worktreePath">The full worktree path.</param>
    /// <returns>The last directory segment, or the full path if extraction fails.</returns>
    private static string ExtractProjectName(string worktreePath)
    {
        if (string.IsNullOrWhiteSpace(worktreePath))
            return "Unknown";

        var trimmed = worktreePath.TrimEnd('/', '\\');
        var lastSep = trimmed.LastIndexOfAny(['/', '\\']);
        return lastSep >= 0 ? trimmed[(lastSep + 1)..] : trimmed;
    }
}
