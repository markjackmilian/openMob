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
/// ViewModel for the ProjectsPage. Manages the list of all projects with
/// selection, deletion, and add-project actions (REQ-022, REQ-023).
/// </summary>
public sealed partial class ProjectsViewModel : ObservableObject
{
    private readonly IProjectService _projectService;
    private readonly INavigationService _navigationService;
    private readonly IAppPopupService _popupService;
    private readonly IActiveProjectService _activeProjectService;

    /// <summary>Initialises the ProjectsViewModel with required dependencies.</summary>
    /// <param name="projectService">Service for project operations.</param>
    /// <param name="navigationService">Service for Shell navigation.</param>
    /// <param name="popupService">Service for popup/dialog operations.</param>
    /// <param name="activeProjectService">Service for managing the client-side active project state.</param>
    public ProjectsViewModel(
        IProjectService projectService,
        INavigationService navigationService,
        IAppPopupService popupService,
        IActiveProjectService activeProjectService)
    {
        ArgumentNullException.ThrowIfNull(projectService);
        ArgumentNullException.ThrowIfNull(navigationService);
        ArgumentNullException.ThrowIfNull(popupService);
        ArgumentNullException.ThrowIfNull(activeProjectService);

        _projectService = projectService;
        _navigationService = navigationService;
        _popupService = popupService;
        _activeProjectService = activeProjectService;
    }

    /// <summary>Gets or sets the collection of project items for display.</summary>
    [ObservableProperty]
    private ObservableCollection<ProjectItem> _projects = [];

    /// <summary>Gets or sets whether the project list is currently loading.</summary>
    [ObservableProperty]
    private bool _isLoading;

    /// <summary>Gets or sets whether the project list is empty.</summary>
    [ObservableProperty]
    private bool _isEmpty;

    /// <summary>Gets or sets the ID of the currently active project.</summary>
    [ObservableProperty]
    private string? _activeProjectId;

    /// <summary>
    /// Loads all projects from the server and maps them to display models.
    /// Sets <see cref="ActiveProjectId"/> from the current project.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    [RelayCommand]
    private async Task LoadProjectsAsync(CancellationToken ct)
    {
#if DEBUG
        var sw = Stopwatch.StartNew();
        DebugLogger.LogCommand(nameof(LoadProjectsAsync), "start");
        try
        {
#endif
        if (IsLoading)
            return;

        IsLoading = true;

        try
        {
            var projects = await _projectService.GetAllProjectsAsync(ct);
            var currentProject = await _activeProjectService.GetActiveProjectAsync(ct);

            ActiveProjectId = currentProject?.Id;

            var items = projects.Select(p => new ProjectItem(
                Id: p.Id,
                Name: ProjectNameHelper.ExtractFromWorktree(p.Worktree),
                Path: p.Worktree,
                IsActive: p.Id == ActiveProjectId
            )).ToList();

            Projects = new ObservableCollection<ProjectItem>(items);
            IsEmpty = Projects.Count == 0;
        }
        catch (Exception ex)
        {
            SentryHelper.CaptureException(ex, new Dictionary<string, object>
            {
                ["context"] = "ProjectsViewModel.LoadProjectsAsync",
            });
            Projects = [];
            IsEmpty = true;
        }
        finally
        {
            IsLoading = false;
        }
#if DEBUG
        sw.Stop();
        DebugLogger.LogCommand(nameof(LoadProjectsAsync), "complete", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            DebugLogger.LogCommand(nameof(LoadProjectsAsync), "failed", error: $"{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            throw;
        }
#endif
    }

    /// <summary>
    /// Navigates to the project detail page for the specified project (REQ-023).
    /// </summary>
    /// <param name="projectId">The project ID to view.</param>
    /// <param name="ct">Cancellation token.</param>
    [RelayCommand]
    private async Task SelectProjectAsync(string projectId, CancellationToken ct)
    {
#if DEBUG
        var sw = Stopwatch.StartNew();
        DebugLogger.LogCommand(nameof(SelectProjectAsync), "start");
        try
        {
#endif
        ArgumentException.ThrowIfNullOrWhiteSpace(projectId);

        await _navigationService.GoToAsync("project-detail", new Dictionary<string, object>
        {
            ["projectId"] = projectId,
        }, ct);
#if DEBUG
        sw.Stop();
        DebugLogger.LogCommand(nameof(SelectProjectAsync), "complete", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            DebugLogger.LogCommand(nameof(SelectProjectAsync), "failed", error: $"{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            throw;
        }
#endif
    }

    /// <summary>
    /// Shows a confirmation dialog and deletes the specified project if confirmed.
    /// </summary>
    /// <param name="projectId">The project ID to delete.</param>
    /// <param name="ct">Cancellation token.</param>
    [RelayCommand]
    private async Task DeleteProjectAsync(string projectId, CancellationToken ct)
    {
#if DEBUG
        var sw = Stopwatch.StartNew();
        DebugLogger.LogCommand(nameof(DeleteProjectAsync), "start");
        try
        {
#endif
        ArgumentException.ThrowIfNullOrWhiteSpace(projectId);

        var confirmed = await _popupService.ShowConfirmDeleteAsync(
            "Delete Project",
            "Are you sure you want to delete this project? This action cannot be undone.",
            ct);

        if (!confirmed)
            return;

        // Note: opencode server manages projects by directory. Deletion is not
        // directly supported via the API in the current version. For now, show
        // a toast indicating the limitation.
        await _popupService.ShowToastAsync("Project deletion is managed by the server.", ct);
        await LoadProjectsAsync(ct);
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

    /// <summary>
    /// Shows the AddProjectSheet popup for creating a new project.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    [RelayCommand]
    private async Task ShowAddProjectAsync(CancellationToken ct)
    {
#if DEBUG
        var sw = Stopwatch.StartNew();
        DebugLogger.LogCommand(nameof(ShowAddProjectAsync), "start");
        try
        {
#endif
        // The popup instance is created and pushed by the MAUI layer.
        // This command signals the intent; the View layer handles the popup lifecycle.
        await _popupService.ShowToastAsync("Add project sheet requested.", ct);
#if DEBUG
        sw.Stop();
        DebugLogger.LogCommand(nameof(ShowAddProjectAsync), "complete", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            DebugLogger.LogCommand(nameof(ShowAddProjectAsync), "failed", error: $"{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            throw;
        }
#endif
    }

}
