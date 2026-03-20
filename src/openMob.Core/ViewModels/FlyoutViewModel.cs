using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using openMob.Core.Helpers;
using openMob.Core.Infrastructure.Http.Dtos.Opencode;
using openMob.Core.Infrastructure.Logging;
using openMob.Core.Infrastructure.Monitoring;
using openMob.Core.Models;
using openMob.Core.Services;

namespace openMob.Core.ViewModels;

/// <summary>
/// ViewModel for the dynamic flyout body. Displays the current project's sessions
/// and provides actions for session selection, deletion, and new chat creation.
/// </summary>
public sealed partial class FlyoutViewModel : ObservableObject
{
    private readonly IProjectService _projectService;
    private readonly ISessionService _sessionService;
    private readonly INavigationService _navigationService;
    private readonly IAppPopupService _popupService;

    /// <summary>Initialises the FlyoutViewModel with required dependencies.</summary>
    /// <param name="projectService">Service for project operations.</param>
    /// <param name="sessionService">Service for session operations.</param>
    /// <param name="navigationService">Service for Shell navigation.</param>
    /// <param name="popupService">Service for popup/dialog operations.</param>
    public FlyoutViewModel(
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

    /// <summary>Gets or sets the current project name displayed as the section title (uppercase).</summary>
    [ObservableProperty]
    private string _projectSectionTitle = string.Empty;

    /// <summary>Gets or sets the sessions for the current project.</summary>
    [ObservableProperty]
    private ObservableCollection<SessionItem> _sessions = [];

    /// <summary>Gets or sets whether a project is currently selected.</summary>
    [ObservableProperty]
    private bool _hasProject;

    /// <summary>Gets or sets whether the session list is currently loading.</summary>
    [ObservableProperty]
    private bool _isLoading;

    // ─── Commands ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Loads the current project and its sessions for display in the flyout.
    /// Sets <see cref="ProjectSectionTitle"/> to the uppercase project name
    /// and populates <see cref="Sessions"/>.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    [RelayCommand]
    private async Task LoadSessionsAsync(CancellationToken ct)
    {
#if DEBUG
        var sw = Stopwatch.StartNew();
        DebugLogger.LogCommand(nameof(LoadSessionsAsync), "start");
        try
        {
#endif
        if (IsLoading)
            return;

        IsLoading = true;

        try
        {
            var currentProject = await _projectService.GetCurrentProjectAsync(ct).ConfigureAwait(false);

            if (currentProject is null)
            {
                HasProject = false;
                ProjectSectionTitle = string.Empty;
                Sessions = [];
                return;
            }

            HasProject = true;
            ProjectSectionTitle = ProjectNameHelper.ExtractFromWorktree(currentProject.Worktree).ToUpperInvariant();

            var sessions = await _sessionService.GetSessionsByProjectAsync(currentProject.Id, ct)
                .ConfigureAwait(false);

            var items = sessions.Select(s => new SessionItem(
                Id: s.Id,
                Title: s.Title,
                ProjectId: s.ProjectId,
                UpdatedAt: DateTimeOffset.FromUnixTimeMilliseconds(s.Time.Updated),
                IsSelected: false
            )).ToList();

            Sessions = new ObservableCollection<SessionItem>(items);
        }
        catch (Exception ex)
        {
            SentryHelper.CaptureException(ex, new Dictionary<string, object>
            {
                ["context"] = "FlyoutViewModel.LoadSessionsAsync",
            });
            Sessions = [];
        }
        finally
        {
            IsLoading = false;
        }
#if DEBUG
        sw.Stop();
        DebugLogger.LogCommand(nameof(LoadSessionsAsync), "complete", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            DebugLogger.LogCommand(nameof(LoadSessionsAsync), "failed", error: $"{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            throw;
        }
#endif
    }

    /// <summary>
    /// Navigates to the ChatPage with the specified session and closes the flyout.
    /// </summary>
    /// <param name="sessionId">The session identifier to navigate to.</param>
    /// <param name="ct">Cancellation token.</param>
    [RelayCommand]
    private async Task SelectSessionAsync(string sessionId, CancellationToken ct)
    {
#if DEBUG
        var sw = Stopwatch.StartNew();
        DebugLogger.LogCommand(nameof(SelectSessionAsync), "start");
        try
        {
#endif
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        await _navigationService.GoToAsync("//chat", new Dictionary<string, object>
        {
            ["sessionId"] = sessionId,
        }, ct);
#if DEBUG
        sw.Stop();
        DebugLogger.LogCommand(nameof(SelectSessionAsync), "complete", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            DebugLogger.LogCommand(nameof(SelectSessionAsync), "failed", error: $"{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            throw;
        }
#endif
    }

    /// <summary>
    /// Shows a confirmation dialog and deletes the specified session if confirmed.
    /// Reloads the session list after deletion.
    /// </summary>
    /// <param name="sessionId">The session identifier to delete.</param>
    /// <param name="ct">Cancellation token.</param>
    [RelayCommand]
    private async Task DeleteSessionAsync(string sessionId, CancellationToken ct)
    {
#if DEBUG
        var sw = Stopwatch.StartNew();
        DebugLogger.LogCommand(nameof(DeleteSessionAsync), "start");
        try
        {
#endif
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        var confirmed = await _popupService.ShowConfirmDeleteAsync(
            "Delete Session",
            "Are you sure you want to delete this session? This action cannot be undone.",
            ct);

        if (!confirmed)
            return;

        try
        {
            var deleted = await _sessionService.DeleteSessionAsync(sessionId, ct).ConfigureAwait(false);

            if (deleted)
            {
                await _popupService.ShowToastAsync("Session deleted.", ct);
                await LoadSessionsAsync(ct);
            }
            else
            {
                await _popupService.ShowErrorAsync("Error", "Failed to delete the session.", ct);
            }
        }
        catch (Exception ex)
        {
            SentryHelper.CaptureException(ex, new Dictionary<string, object>
            {
                ["context"] = "FlyoutViewModel.DeleteSessionAsync",
                ["sessionId"] = sessionId,
            });
            await _popupService.ShowErrorAsync("Error", "Failed to delete the session.", ct);
        }
#if DEBUG
        sw.Stop();
        DebugLogger.LogCommand(nameof(DeleteSessionAsync), "complete", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            DebugLogger.LogCommand(nameof(DeleteSessionAsync), "failed", error: $"{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            throw;
        }
#endif
    }

    /// <summary>
    /// Creates a new session and navigates to the ChatPage.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    [RelayCommand]
    private async Task NewChatAsync(CancellationToken ct)
    {
#if DEBUG
        var sw = Stopwatch.StartNew();
        DebugLogger.LogCommand(nameof(NewChatAsync), "start");
        try
        {
#endif
        try
        {
            var session = await _sessionService.CreateSessionAsync(null, ct).ConfigureAwait(false);

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
        catch (Exception ex)
        {
            SentryHelper.CaptureException(ex, new Dictionary<string, object>
            {
                ["context"] = "FlyoutViewModel.NewChatAsync",
            });
            await _popupService.ShowErrorAsync("Error", "Failed to create a new session.", ct);
        }
#if DEBUG
        sw.Stop();
        DebugLogger.LogCommand(nameof(NewChatAsync), "complete", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            DebugLogger.LogCommand(nameof(NewChatAsync), "failed", error: $"{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            throw;
        }
#endif
    }
}
