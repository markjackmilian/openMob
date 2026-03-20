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
/// ViewModel for the ProjectSwitcherSheet popup. Allows rapid project switching
/// from the ChatPage header (REQ-028, REQ-029).
/// </summary>
/// <remarks>
/// Selecting a different project resumes the last session of that project.
/// If no sessions exist, a new empty session is created.
/// </remarks>
public sealed partial class ProjectSwitcherViewModel : ObservableObject
{
    private readonly IProjectService _projectService;
    private readonly ISessionService _sessionService;
    private readonly INavigationService _navigationService;
    private readonly IAppPopupService _popupService;

    /// <summary>Initialises the ProjectSwitcherViewModel with required dependencies.</summary>
    /// <param name="projectService">Service for project operations.</param>
    /// <param name="sessionService">Service for session operations.</param>
    /// <param name="navigationService">Service for Shell navigation.</param>
    /// <param name="popupService">Service for popup/dialog operations.</param>
    public ProjectSwitcherViewModel(
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

    /// <summary>Gets or sets the collection of project items for display.</summary>
    [ObservableProperty]
    private ObservableCollection<ProjectItem> _projects = [];

    /// <summary>Gets or sets the ID of the currently active project.</summary>
    [ObservableProperty]
    private string? _activeProjectId;

    /// <summary>Gets or sets whether the project list is currently loading.</summary>
    [ObservableProperty]
    private bool _isLoading;

    /// <summary>
    /// Loads all projects and identifies the active one (REQ-028).
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
            var currentProject = await _projectService.GetCurrentProjectAsync(ct);

            ActiveProjectId = currentProject?.Id;

            var items = projects.Select(p => new ProjectItem(
                Id: p.Id,
                Name: ProjectNameHelper.ExtractFromWorktree(p.Worktree),
                Path: p.Worktree,
                IsActive: p.Id == ActiveProjectId
            )).ToList();

            Projects = new ObservableCollection<ProjectItem>(items);
        }
        catch (Exception ex)
        {
            SentryHelper.CaptureException(ex, new Dictionary<string, object>
            {
                ["context"] = "ProjectSwitcherViewModel.LoadProjectsAsync",
            });
            Projects = [];
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
    /// Selects a project, resumes its last session (or creates a new one),
    /// closes the popup, and navigates to chat (REQ-029).
    /// </summary>
    /// <param name="projectId">The project ID to switch to.</param>
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

        try
        {
            // Close the popup first
            await _popupService.PopPopupAsync(ct);

            // Find the last session for this project
            var lastSession = await _sessionService.GetLastSessionForProjectAsync(projectId, ct);

            if (lastSession is not null)
            {
                // Resume the last session
                await _navigationService.GoToAsync("//chat", new Dictionary<string, object>
                {
                    ["sessionId"] = lastSession.Id,
                }, ct);
            }
            else
            {
                // Create a new empty session
                var newSession = await _sessionService.CreateSessionAsync(null, ct);

                if (newSession is not null)
                {
                    await _navigationService.GoToAsync("//chat", new Dictionary<string, object>
                    {
                        ["sessionId"] = newSession.Id,
                    }, ct);
                }
                else
                {
                    await _navigationService.GoToAsync("//chat", ct);
                }
            }
        }
        catch (Exception ex)
        {
            SentryHelper.CaptureException(ex, new Dictionary<string, object>
            {
                ["context"] = "ProjectSwitcherViewModel.SelectProjectAsync",
                ["projectId"] = projectId,
            });
        }
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
    /// Closes the popup and navigates to the ProjectsPage for full project management.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    [RelayCommand]
    private async Task ManageProjectsAsync(CancellationToken ct)
    {
#if DEBUG
        var sw = Stopwatch.StartNew();
        DebugLogger.LogCommand(nameof(ManageProjectsAsync), "start");
        try
        {
#endif
        await _popupService.PopPopupAsync(ct);
        await _navigationService.GoToAsync("projects", ct);
#if DEBUG
        sw.Stop();
        DebugLogger.LogCommand(nameof(ManageProjectsAsync), "complete", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            DebugLogger.LogCommand(nameof(ManageProjectsAsync), "failed", error: $"{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            throw;
        }
#endif
    }

}
