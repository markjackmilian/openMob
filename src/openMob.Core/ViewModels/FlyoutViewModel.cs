using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using openMob.Core.Helpers;
using openMob.Core.Infrastructure.Logging;
using openMob.Core.Infrastructure.Monitoring;
using openMob.Core.Messages;
using openMob.Core.Models;
using openMob.Core.Services;

namespace openMob.Core.ViewModels;

/// <summary>
/// ViewModel for the dynamic flyout body. Displays the current project's sessions
/// and provides actions for session selection and new chat creation.
/// Subscribes to <see cref="SessionDeletedMessage"/> to auto-refresh the session list
/// and to <see cref="CurrentSessionChangedMessage"/> to highlight the active session.
/// </summary>
/// <remarks>
/// Registered as Singleton — a single instance is shared between <c>FlyoutHeaderView</c>
/// and <c>FlyoutContentView</c>. Implements <see cref="IDisposable"/> to unregister
/// all <see cref="WeakReferenceMessenger"/> subscriptions on app shutdown.
/// </remarks>
public sealed partial class FlyoutViewModel : ObservableObject, IDisposable
{
    private readonly IProjectService _projectService;
    private readonly ISessionService _sessionService;
    private readonly INavigationService _navigationService;
    private readonly IAppPopupService _popupService;
    private readonly IDispatcherService _dispatcher;
    private readonly IActiveProjectService _activeProjectService;

    /// <summary>
    /// Used to signal in-flight fire-and-forget tasks (e.g. <see cref="LoadSessionsCommand"/>
    /// triggered by <see cref="SessionDeletedMessage"/>) that the ViewModel has been disposed.
    /// </summary>
    private readonly CancellationTokenSource _disposeCts = new();

    /// <summary>Initialises the FlyoutViewModel with required dependencies.</summary>
    /// <param name="projectService">Service for project operations.</param>
    /// <param name="sessionService">Service for session operations.</param>
    /// <param name="navigationService">Service for Shell navigation.</param>
    /// <param name="popupService">Service for popup/dialog operations.</param>
    /// <param name="dispatcher">UI thread dispatcher for thread-safe collection updates.</param>
    /// <param name="activeProjectService">Service for managing the client-side active project state.</param>
    public FlyoutViewModel(
        IProjectService projectService,
        ISessionService sessionService,
        INavigationService navigationService,
        IAppPopupService popupService,
        IDispatcherService dispatcher,
        IActiveProjectService activeProjectService)
    {
        ArgumentNullException.ThrowIfNull(projectService);
        ArgumentNullException.ThrowIfNull(sessionService);
        ArgumentNullException.ThrowIfNull(navigationService);
        ArgumentNullException.ThrowIfNull(popupService);
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(activeProjectService);

        _projectService = projectService;
        _sessionService = sessionService;
        _navigationService = navigationService;
        _popupService = popupService;
        _dispatcher = dispatcher;
        _activeProjectService = activeProjectService;

        // Subscribe to session deletion — refresh the list when any session is deleted.
        // Fire-and-forget is safe here: LoadSessionsAsync has its own internal catch that
        // handles all exceptions, so no unobserved exception can escape.
        WeakReferenceMessenger.Default.Register<SessionDeletedMessage>(
            this,
            (_, _) =>
            {
                if (_disposeCts.IsCancellationRequested)
                    return;
                _ = LoadSessionsCommand.ExecuteAsync(null);
            });

        // Subscribe to active session changes — highlight the current session in the list.
        // Sessions[i] is mutated on the UI thread via _dispatcher to avoid cross-thread exceptions.
        WeakReferenceMessenger.Default.Register<CurrentSessionChangedMessage>(
            this,
            (_, message) =>
            {
                CurrentSessionId = message.SessionId;
                _dispatcher.Dispatch(() =>
                {
                    for (var i = 0; i < Sessions.Count; i++)
                    {
                        var s = Sessions[i];
                        if (s.IsSelected != (s.Id == message.SessionId))
                            Sessions[i] = s with { IsSelected = s.Id == message.SessionId };
                    }
                });
            });

        // Subscribe to active project changes — reload sessions when the user switches projects.
        // Fire-and-forget is safe here: LoadSessionsAsync has its own internal catch that
        // handles all exceptions, so no unobserved exception can escape.
        WeakReferenceMessenger.Default.Register<ActiveProjectChangedMessage>(
            this,
            (_, _) =>
            {
                if (_disposeCts.IsCancellationRequested)
                    return;
                _ = LoadSessionsCommand.ExecuteAsync(null);
            });
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

    /// <summary>Gets or sets the identifier of the currently active session, used to highlight it in the list.</summary>
    [ObservableProperty]
    private string? _currentSessionId;

    // ─── Commands ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Loads the current project and its sessions for display in the flyout.
    /// Sets <see cref="ProjectSectionTitle"/> to the uppercase project name
    /// and populates <see cref="Sessions"/>.
    /// All <see cref="Sessions"/> assignments are dispatched to the UI thread.
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
            var currentProject = await _activeProjectService.GetActiveProjectAsync(ct).ConfigureAwait(false);

            if (currentProject is null)
            {
                HasProject = false;
                ProjectSectionTitle = string.Empty;
                _dispatcher.Dispatch(() => Sessions = []);
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
                IsSelected: s.Id == CurrentSessionId
            )).ToList();

            _dispatcher.Dispatch(() => Sessions = new ObservableCollection<SessionItem>(items));
        }
        catch (Exception ex)
        {
            SentryHelper.CaptureException(ex, new Dictionary<string, object>
            {
                ["context"] = "FlyoutViewModel.LoadSessionsAsync",
            });
            _dispatcher.Dispatch(() => Sessions = []);
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
    /// If the tapped session is already the active one, closes the drawer without re-navigating.
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

        if (sessionId == CurrentSessionId)
        {
            await _navigationService.CloseFlyoutAsync(ct);
            return;
        }

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

    // ─── IDisposable ──────────────────────────────────────────────────────────

    /// <summary>
    /// Cancels any in-flight fire-and-forget tasks, then unregisters all
    /// <see cref="WeakReferenceMessenger"/> subscriptions.
    /// Called by the DI container on app shutdown (Singleton lifetime).
    /// </summary>
    public void Dispose()
    {
        _disposeCts.Cancel();
        _disposeCts.Dispose();
        WeakReferenceMessenger.Default.UnregisterAll(this);
    }
}
