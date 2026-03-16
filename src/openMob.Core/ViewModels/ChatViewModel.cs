using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using openMob.Core.Helpers;
using openMob.Core.Infrastructure.Http;
using openMob.Core.Infrastructure.Monitoring;
using openMob.Core.Models;
using openMob.Core.Services;

namespace openMob.Core.ViewModels;

/// <summary>
/// ViewModel for the ChatPage header bar and More Menu.
/// Drives REQ-012 through REQ-016 (header display, project switcher trigger,
/// new chat, more menu actions, and status banner).
/// Does NOT handle the message list or input bar (future spec).
/// </summary>
public sealed partial class ChatViewModel : ObservableObject
{
    private readonly IProjectService _projectService;
    private readonly ISessionService _sessionService;
    private readonly INavigationService _navigationService;
    private readonly IAppPopupService _popupService;
    private readonly IOpencodeConnectionManager _connectionManager;
    private readonly IProviderService _providerService;
    private readonly IProjectPreferenceService _preferenceService;

    /// <summary>Initialises the ChatViewModel with required dependencies.</summary>
    /// <param name="projectService">Service for project operations.</param>
    /// <param name="sessionService">Service for session operations.</param>
    /// <param name="navigationService">Service for Shell navigation.</param>
    /// <param name="popupService">Service for popup/dialog operations.</param>
    /// <param name="connectionManager">Manages the opencode server connection state.</param>
    /// <param name="providerService">Service for AI provider operations.</param>
    /// <param name="preferenceService">Service for per-project preference persistence.</param>
    public ChatViewModel(
        IProjectService projectService,
        ISessionService sessionService,
        INavigationService navigationService,
        IAppPopupService popupService,
        IOpencodeConnectionManager connectionManager,
        IProviderService providerService,
        IProjectPreferenceService preferenceService)
    {
        ArgumentNullException.ThrowIfNull(projectService);
        ArgumentNullException.ThrowIfNull(sessionService);
        ArgumentNullException.ThrowIfNull(navigationService);
        ArgumentNullException.ThrowIfNull(popupService);
        ArgumentNullException.ThrowIfNull(connectionManager);
        ArgumentNullException.ThrowIfNull(providerService);
        ArgumentNullException.ThrowIfNull(preferenceService);

        _projectService = projectService;
        _sessionService = sessionService;
        _navigationService = navigationService;
        _popupService = popupService;
        _connectionManager = connectionManager;
        _providerService = providerService;
        _preferenceService = preferenceService;
    }

    // ─── Properties ───────────────────────────────────────────────────────────

    /// <summary>Gets or sets the current project display name.</summary>
    [ObservableProperty]
    private string _projectName = "No project";

    /// <summary>Gets or sets the current session title.</summary>
    [ObservableProperty]
    private string _sessionName = "New chat";

    /// <summary>Gets or sets the active session identifier.</summary>
    [ObservableProperty]
    private string? _currentSessionId;

    /// <summary>Gets or sets the active project identifier.</summary>
    [ObservableProperty]
    private string? _currentProjectId;

    /// <summary>Gets or sets whether the opencode server is offline.</summary>
    [ObservableProperty]
    private bool _isServerOffline;

    /// <summary>Gets or sets whether no AI provider is configured.</summary>
    [ObservableProperty]
    private bool _hasNoProvider;

    /// <summary>Gets or sets the status banner info computed from server/provider state.</summary>
    [ObservableProperty]
    private StatusBannerInfo? _statusBanner;

    /// <summary>
    /// Gets or sets the currently selected model identifier in "providerId/modelId" format (REQ-008, REQ-011).
    /// Splittable with <c>Split('/', 2)</c> to obtain ProviderId and ModelId for <c>SendPromptRequest</c>.
    /// </summary>
    [ObservableProperty]
    private string? _selectedModelId;

    /// <summary>
    /// Gets or sets the display name of the currently selected model (REQ-008).
    /// Derived from <see cref="SelectedModelId"/> by extracting the part after the first '/'.
    /// Null when no model is selected — the UI should show a placeholder.
    /// </summary>
    [ObservableProperty]
    private string? _selectedModelName;

    // ─── Commands ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Loads the current project and session info, subscribes to connection status changes,
    /// and evaluates the status banner state (REQ-012, REQ-016).
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    [RelayCommand]
    private async Task LoadContextAsync(CancellationToken ct)
    {
        try
        {
            // Subscribe to connection status changes for status banner updates
            _connectionManager.StatusChanged -= OnConnectionStatusChanged;
            _connectionManager.StatusChanged += OnConnectionStatusChanged;

            // Load current project
            var currentProject = await _projectService.GetCurrentProjectAsync(ct).ConfigureAwait(false);
            if (currentProject is not null)
            {
                CurrentProjectId = currentProject.Id;
                ProjectName = ProjectNameHelper.ExtractFromWorktree(currentProject.Worktree);

                // Load default model preference for this project (REQ-008)
                var pref = await _preferenceService.GetAsync(currentProject.Id, ct).ConfigureAwait(false);
                if (pref?.DefaultModelId is not null)
                {
                    SelectedModelId = pref.DefaultModelId;
                    SelectedModelName = ModelIdHelper.ExtractModelName(pref.DefaultModelId);
                }
            }
            else
            {
                CurrentProjectId = null;
                ProjectName = "No project";
            }

            // Load current session if set
            if (CurrentSessionId is not null)
            {
                var session = await _sessionService.GetSessionAsync(CurrentSessionId, ct).ConfigureAwait(false);
                if (session is not null)
                {
                    SessionName = session.Title;
                }
            }

            // Evaluate provider state
            HasNoProvider = !await _providerService.HasAnyProviderConfiguredAsync(ct).ConfigureAwait(false);

            // Evaluate server connection state
            IsServerOffline = _connectionManager.ConnectionStatus
                is ServerConnectionStatus.Disconnected
                or ServerConnectionStatus.Error;

            UpdateStatusBanner();
        }
        catch (Exception ex)
        {
            SentryHelper.CaptureException(ex, new Dictionary<string, object>
            {
                ["context"] = "ChatViewModel.LoadContextAsync",
            });
        }
    }

    /// <summary>
    /// Signals intent to open the ProjectSwitcherSheet popup (REQ-013).
    /// The View layer handles popup creation and presentation.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    [RelayCommand]
    private async Task OpenProjectSwitcherAsync(CancellationToken ct)
    {
        // The View layer creates and pushes the ProjectSwitcherSheet popup.
        // This command signals the intent for testability.
        await Task.CompletedTask;
    }

    /// <summary>
    /// Creates a new session via <see cref="ISessionService"/> and updates the
    /// current session context (REQ-014).
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    [RelayCommand]
    private async Task NewChatAsync(CancellationToken ct)
    {
        try
        {
            var session = await _sessionService.CreateSessionAsync(null, ct).ConfigureAwait(false);

            if (session is not null)
            {
                CurrentSessionId = session.Id;
                SessionName = session.Title;
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
                ["context"] = "ChatViewModel.NewChatAsync",
            });
            await _popupService.ShowErrorAsync("Error", "Failed to create a new session.", ct);
        }
    }

    /// <summary>
    /// Shows the More Menu option sheet and routes the selected action (REQ-015).
    /// Options: Rename session, Change agent, Change model, Fork session, Archive, Delete.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    [RelayCommand]
    private async Task ShowMoreMenuAsync(CancellationToken ct)
    {
        var options = new List<string>
        {
            "Rename session",
            "Change agent",
            "Change model",
            "Fork session",
            "Archive",
            "Delete",
        };

        var selected = await _popupService.ShowOptionSheetAsync("More", options, ct);

        if (selected is null)
            return;

        switch (selected)
        {
            case "Rename session":
                await HandleRenameSessionAsync(ct);
                break;

            case "Change agent":
                // Signal intent — the View layer handles the AgentPickerSheet popup.
                break;

            case "Change model":
                // Open the model picker and update in-memory selection (REQ-009).
                // No persistence to SQLite — this is a session-level override.
                await _popupService.ShowModelPickerAsync(modelId =>
                {
                    SelectedModelId = modelId;
                    SelectedModelName = ModelIdHelper.ExtractModelName(modelId);
                }, ct).ConfigureAwait(false);
                break;

            case "Fork session":
                await HandleForkSessionAsync(ct);
                break;

            case "Archive":
                await HandleArchiveAsync(ct);
                break;

            case "Delete":
                await HandleDeleteAsync(ct);
                break;
        }
    }

    // ─── Private action handlers ──────────────────────────────────────────────

    /// <summary>Handles the "Rename session" action from the More Menu.</summary>
    private async Task HandleRenameSessionAsync(CancellationToken ct)
    {
        if (CurrentSessionId is null)
            return;

        var newName = await _popupService.ShowRenameAsync(SessionName, ct);

        if (newName is null || newName == SessionName)
            return;

        var success = await _sessionService.UpdateSessionTitleAsync(CurrentSessionId, newName, ct)
            .ConfigureAwait(false);

        if (success)
        {
            SessionName = newName;
            await _popupService.ShowToastAsync("Session renamed.", ct);
        }
        else
        {
            await _popupService.ShowErrorAsync("Error", "Failed to rename the session.", ct);
        }
    }

    /// <summary>Handles the "Fork session" action from the More Menu.</summary>
    private async Task HandleForkSessionAsync(CancellationToken ct)
    {
        if (CurrentSessionId is null)
            return;

        try
        {
            var forkedSession = await _sessionService.ForkSessionAsync(CurrentSessionId, ct)
                .ConfigureAwait(false);

            if (forkedSession is not null)
            {
                CurrentSessionId = forkedSession.Id;
                SessionName = forkedSession.Title;
                await _popupService.ShowToastAsync("Session forked.", ct);
            }
            else
            {
                await _popupService.ShowErrorAsync("Error", "Failed to fork the session.", ct);
            }
        }
        catch (Exception ex)
        {
            SentryHelper.CaptureException(ex, new Dictionary<string, object>
            {
                ["context"] = "ChatViewModel.HandleForkSessionAsync",
                ["sessionId"] = CurrentSessionId ?? "null",
            });
            await _popupService.ShowErrorAsync("Error", "Failed to fork the session.", ct);
        }
    }

    /// <summary>Handles the "Archive" action from the More Menu (stub for now).</summary>
    private async Task HandleArchiveAsync(CancellationToken ct)
    {
        var confirmed = await _popupService.ShowConfirmDeleteAsync(
            "Archive Session",
            "Are you sure you want to archive this session?",
            ct);

        if (!confirmed)
            return;

        // Archive logic is a stub — the opencode API does not yet support archiving.
        await _popupService.ShowToastAsync("Session archived.", ct);
    }

    /// <summary>Handles the "Delete" action from the More Menu.</summary>
    private async Task HandleDeleteAsync(CancellationToken ct)
    {
        if (CurrentSessionId is null)
            return;

        var confirmed = await _popupService.ShowConfirmDeleteAsync(
            "Delete Session",
            "Are you sure you want to delete this session? This action cannot be undone.",
            ct);

        if (!confirmed)
            return;

        try
        {
            var deleted = await _sessionService.DeleteSessionAsync(CurrentSessionId, ct)
                .ConfigureAwait(false);

            if (deleted)
            {
                // Create a new session to replace the deleted one
                var newSession = await _sessionService.CreateSessionAsync(null, ct)
                    .ConfigureAwait(false);

                if (newSession is not null)
                {
                    CurrentSessionId = newSession.Id;
                    SessionName = newSession.Title;
                }
                else
                {
                    CurrentSessionId = null;
                    SessionName = "New chat";
                }

                await _popupService.ShowToastAsync("Session deleted.", ct);
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
                ["context"] = "ChatViewModel.HandleDeleteAsync",
                ["sessionId"] = CurrentSessionId ?? "null",
            });
            await _popupService.ShowErrorAsync("Error", "Failed to delete the session.", ct);
        }
    }

    // ─── Status banner logic (REQ-016) ────────────────────────────────────────

    /// <summary>Handles connection status changes from <see cref="IOpencodeConnectionManager"/>.</summary>
    /// <param name="newStatus">The new server connection status.</param>
    private void OnConnectionStatusChanged(ServerConnectionStatus newStatus)
    {
        IsServerOffline = newStatus
            is ServerConnectionStatus.Disconnected
            or ServerConnectionStatus.Error;

        UpdateStatusBanner();
    }

    /// <summary>
    /// Evaluates the current server and provider state and updates <see cref="StatusBanner"/>.
    /// Priority: server offline > no provider > clear.
    /// </summary>
    private void UpdateStatusBanner()
    {
        if (IsServerOffline)
        {
            StatusBanner = new StatusBannerInfo(
                StatusBannerType.ServerOffline,
                "Server non raggiungibile — modalità offline",
                ActionLabel: null,
                IsDismissible: false);
        }
        else if (HasNoProvider)
        {
            StatusBanner = new StatusBannerInfo(
                StatusBannerType.NoProvider,
                "Nessun provider AI configurato",
                ActionLabel: "Configura",
                IsDismissible: false);
        }
        else
        {
            StatusBanner = null;
        }
    }

}
