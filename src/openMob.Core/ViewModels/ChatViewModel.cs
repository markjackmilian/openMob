using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using openMob.Core.Helpers;
using openMob.Core.Infrastructure.Http;
using openMob.Core.Infrastructure.Http.Dtos.Opencode;
using openMob.Core.Infrastructure.Logging;
using openMob.Core.Infrastructure.Monitoring;
using openMob.Core.Messages;
using openMob.Core.Models;
using openMob.Core.Services;

namespace openMob.Core.ViewModels;

/// <summary>
/// ViewModel for the ChatPage. Drives the full conversational loop:
/// header bar, status banner, message history, message sending with optimistic UI,
/// SSE streaming of AI responses, cancellation, suggestion chips, and error handling.
/// </summary>
/// <remarks>
/// <para>
/// Existing functionality (header display, project switcher, new chat, more menu,
/// status banner, model selection) is preserved from previous specs.
/// </para>
/// <para>
/// SSE events arrive on background threads. All <see cref="Messages"/> mutations
/// are dispatched to the UI thread via <see cref="IDispatcherService"/> for thread safety.
/// </para>
/// </remarks>
public sealed partial class ChatViewModel : ObservableObject, IDisposable
{
    private readonly IProjectService _projectService;
    private readonly ISessionService _sessionService;
    private readonly INavigationService _navigationService;
    private readonly IAppPopupService _popupService;
    private readonly IOpencodeConnectionManager _connectionManager;
    private readonly IProjectPreferenceService _preferenceService;
    private readonly IChatService _chatService;
    private readonly IOpencodeApiClient _apiClient;
    private readonly IDispatcherService _dispatcher;
    private readonly IActiveProjectService _activeProjectService;
    private readonly IHeartbeatMonitorService _heartbeatMonitor;

    /// <summary>Cancellation token source for the active SSE subscription.</summary>
    private CancellationTokenSource? _sseCts;

    /// <summary>Cancellation token source for the active reconnection loop. Cancelled on dispose or state recovery.</summary>
    private CancellationTokenSource? _reconnectionCts;

    /// <summary>
    /// Guards against showing the reconnection modal twice.
    /// Set to <c>true</c> when the modal is pushed; reset to <c>false</c> when it is popped
    /// (either by <see cref="ReconnectingModalViewModel.ReconnectionSucceeded"/> or by the
    /// user navigating to ServerManagementPage via the "Gestisci server" button).
    /// </summary>
    private volatile bool _isReconnectingModalVisible;

    /// <summary>
    /// Tracks the previous health state to detect <c>Lost/Degraded → Healthy</c> transitions.
    /// Initialised to <see cref="ConnectionHealthState.Healthy"/> so the first heartbeat
    /// does not trigger a spurious replay.
    /// </summary>
    private volatile ConnectionHealthState _previousHealthState = ConnectionHealthState.Healthy;

    /// <summary>
    /// The absolute path of the current project's working directory, used to filter
    /// incoming SSE events by project context (REQ-004). Set once during
    /// <see cref="LoadContextAsync"/> from <see cref="IActiveProjectService.GetCachedWorktree"/>.
    /// </summary>
    private string? _currentProjectDirectory;

    /// <summary>Tracks the number of pending permission cards in the current message list.</summary>
    private int _pendingPermissionCount;

    /// <summary>
    /// Tracks permission request IDs for which an API reply call is currently in-flight.
    /// Prevents duplicate concurrent API calls on rapid double-tap.
    /// </summary>
    private readonly HashSet<string> _inFlightPermissionReplies = new(StringComparer.Ordinal);

    /// <summary>Tracks the number of pending question cards in the current message list.</summary>
    private int _pendingQuestionCount;

    /// <summary>
    /// Tracks question request IDs for which an API answer call is currently in-flight.
    /// Prevents duplicate concurrent API calls on rapid double-tap.
    /// </summary>
    private readonly HashSet<string> _inFlightQuestionAnswers = new(StringComparer.Ordinal);

    /// <summary>Initialises the ChatViewModel with required dependencies.</summary>
    /// <param name="projectService">Service for project operations.</param>
    /// <param name="sessionService">Service for session operations.</param>
    /// <param name="navigationService">Service for Shell navigation.</param>
    /// <param name="popupService">Service for popup/dialog operations.</param>
    /// <param name="connectionManager">Manages the opencode server connection state.</param>
    /// <param name="preferenceService">Service for per-project preference persistence.</param>
    /// <param name="chatService">Service for chat operations (messages, prompts, SSE).</param>
    /// <param name="apiClient">Low-level opencode API client (used for session abort).</param>
    /// <param name="dispatcher">UI thread dispatcher for thread-safe collection updates.</param>
    /// <param name="activeProjectService">Service for managing the client-side active project state.</param>
    /// <param name="heartbeatMonitor">Service for monitoring server heartbeat health state.</param>
    public ChatViewModel(
        IProjectService projectService,
        ISessionService sessionService,
        INavigationService navigationService,
        IAppPopupService popupService,
        IOpencodeConnectionManager connectionManager,
        IProjectPreferenceService preferenceService,
        IChatService chatService,
        IOpencodeApiClient apiClient,
        IDispatcherService dispatcher,
        IActiveProjectService activeProjectService,
        IHeartbeatMonitorService heartbeatMonitor)
    {
        ArgumentNullException.ThrowIfNull(projectService);
        ArgumentNullException.ThrowIfNull(sessionService);
        ArgumentNullException.ThrowIfNull(navigationService);
        ArgumentNullException.ThrowIfNull(popupService);
        ArgumentNullException.ThrowIfNull(connectionManager);
        ArgumentNullException.ThrowIfNull(preferenceService);
        ArgumentNullException.ThrowIfNull(chatService);
        ArgumentNullException.ThrowIfNull(apiClient);
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(activeProjectService);
        ArgumentNullException.ThrowIfNull(heartbeatMonitor);

        _projectService = projectService;
        _sessionService = sessionService;
        _navigationService = navigationService;
        _popupService = popupService;
        _connectionManager = connectionManager;
        _preferenceService = preferenceService;
        _chatService = chatService;
        _apiClient = apiClient;
        _dispatcher = dispatcher;
        _activeProjectService = activeProjectService;
        _heartbeatMonitor = heartbeatMonitor;

        _heartbeatMonitor.HealthStateChanged += OnHealthStateChanged;

        // Subscribe to project preference changes published by ContextSheetViewModel [REQ-009]
        WeakReferenceMessenger.Default.Register<ProjectPreferenceChangedMessage>(
            this,
            (_, message) =>
            {
                if (message.ProjectId != CurrentProjectId)
                    return;

                var pref = message.UpdatedPreference;

                _dispatcher.Dispatch(() =>
                {
                    if (pref.DefaultModelId is not null)
                    {
                        SelectedModelId = pref.DefaultModelId;
                        SelectedModelName = ModelIdHelper.ExtractModelName(pref.DefaultModelId);
                    }
                    else
                    {
                        SelectedModelId = null;
                        SelectedModelName = null;
                    }

                    // Update agent properties [REQ-007]
                    SelectedAgentName = pref.AgentName;

                    // Update thinking level and auto-accept [REQ-003, REQ-005]
                    ThinkingLevel = pref.ThinkingLevel;
                    AutoAccept = pref.AutoAccept;

                    // Update show-unhandled-SSE-events toggle [REQ-008]
                    ShowUnhandledSseEvents = pref.ShowUnhandledSseEvents;
                });
            });

        // Subscribe to active project changes — navigate to the most recent session
        // of the new project, or to a new session if none exist (REQ-009).
        WeakReferenceMessenger.Default.Register<ActiveProjectChangedMessage>(
            this,
            (r, m) =>
            {
                var vm = (ChatViewModel)r;
                _ = vm.HandleActiveProjectChangedAsync(m);
            });

        // Subscribe to composed messages from MessageComposerSheet [REQ-023, REQ-024]
        WeakReferenceMessenger.Default.Register<MessageComposedMessage>(
            this,
            (r, m) =>
            {
                var vm = (ChatViewModel)r;
                vm._dispatcher.Dispatch(() => _ = vm.HandleMessageComposedAsync(m));
            });

        // Populate default suggestion chips [REQ-017]
        SuggestionChips = new ObservableCollection<SuggestionChip>
        {
            new("Spiega questo codice", "Analisi dettagliata", "Spiega questo codice in dettaglio"),
            new("Trova i bug", "Revisione critica", "Trova eventuali bug o problemi in questo codice"),
            new("Scrivi i test", "Unit test completi", "Scrivi unit test completi per questo codice"),
            new("Refactoring", "Migliora la struttura", "Suggerisci un refactoring per migliorare questo codice"),
        };
    }

    // ─── Existing Properties ──────────────────────────────────────────────────

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

    /// <summary>Gets or sets the current connection health state, driven by heartbeat events.</summary>
    [ObservableProperty]
    private ConnectionHealthState _connectionHealthState = ConnectionHealthState.Healthy;

    /// <summary>Gets or sets the display name of the currently active server connection.</summary>
    [ObservableProperty]
    private string _activeServerName = string.Empty;

    /// <summary>
    /// Gets or sets the currently selected model identifier in "providerId/modelId" format.
    /// Splittable with <c>Split('/', 2)</c> to obtain ProviderId and ModelId for <c>SendPromptRequest</c>.
    /// </summary>
    [ObservableProperty]
    private string? _selectedModelId;

    /// <summary>
    /// Gets or sets the display name of the currently selected model.
    /// Derived from <see cref="SelectedModelId"/> by extracting the part after the first '/'.
    /// Null when no model is selected — the UI should show a placeholder.
    /// </summary>
    [ObservableProperty]
    private string? _selectedModelName;

    /// <summary>
    /// Gets or sets the raw agent name from the project preference.
    /// <c>null</c> means the default agent is active.
    /// Updated during <see cref="LoadContextAsync"/> and when a
    /// <see cref="ProjectPreferenceChangedMessage"/> arrives.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedAgentDisplayName))]
    private string? _selectedAgentName;

    /// <summary>
    /// Gets the display name for the selected agent.
    /// Returns <c>"build"</c> when <see cref="SelectedAgentName"/> is <c>null</c>,
    /// reflecting the opencode server default agent.
    /// </summary>
    public string SelectedAgentDisplayName => SelectedAgentName ?? "build";

    // ─── Chat Properties [REQ-004] ────────────────────────────────────────────

    /// <summary>Gets or sets the collection of chat messages in the current session.</summary>
    [ObservableProperty]
    private ObservableCollection<ChatMessage> _messages = new();

    /// <summary>Gets or sets the current text in the input bar.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendMessageCommand))]
    private string _inputText = string.Empty;

    /// <summary>Gets or sets whether a loading operation (e.g. message history) is in progress.</summary>
    [ObservableProperty]
    private bool _isBusy;

    /// <summary>Gets or sets whether the AI is currently generating a response.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendMessageCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelResponseCommand))]
    private bool _isAiResponding;

    /// <summary>Gets or sets whether the message list is empty.</summary>
    [ObservableProperty]
    private bool _isEmpty = true;

    /// <summary>Gets or sets the current error message, or <c>null</c> if no error.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _errorMessage;

    /// <summary>Gets or sets the collection of suggestion chips displayed when the chat is empty.</summary>
    [ObservableProperty]
    private ObservableCollection<SuggestionChip> _suggestionChips = new();

    /// <summary>Gets whether an error message is currently displayed.</summary>
    public bool HasError => ErrorMessage is not null;

    /// <summary>Gets or sets whether at least one permission card is pending.</summary>
    [ObservableProperty]
    private bool _hasPendingPermissions;

    /// <summary>Gets or sets whether at least one question card is pending.</summary>
    [ObservableProperty]
    private bool _hasPendingQuestions;

    // ─── Chat Page Redesign Properties [REQ-019, REQ-022, REQ-028, REQ-032] ──

    /// <summary>Gets or sets the current thinking/reasoning level, synced from Context Sheet.</summary>
    [ObservableProperty]
    private ThinkingLevel _thinkingLevel = ThinkingLevel.Medium;

    /// <summary>Gets or sets whether auto-accept is enabled for agent suggestions.</summary>
    [ObservableProperty]
    private bool _autoAccept;

    /// <summary>
    /// Gets or sets whether unhandled SSE event debug cards are shown in the chat.
    /// Updated from <see cref="ProjectPreferenceChangedMessage"/> and on initial load.
    /// Default is <c>false</c> — cards are hidden until the user enables the toggle.
    /// </summary>
    [ObservableProperty]
    private bool _showUnhandledSseEvents;

    /// <summary>Gets or sets whether a subagent is currently active (streaming messages).</summary>
    [ObservableProperty]
    private bool _isSubagentActive;

    /// <summary>Gets or sets the display name of the active subagent.</summary>
    [ObservableProperty]
    private string _subagentName = string.Empty;

    /// <summary>
    /// Gets or sets whether the context status bar is visible (REQ-022).
    /// Collapses when scrolling down, reappears when scrolling up or at top.
    /// Always visible when the message list is empty (REQ-037).
    /// </summary>
    [ObservableProperty]
    private bool _isContextBarVisible = true;

    // ─── Session Navigation [REQ-005] ─────────────────────────────────────────

    /// <summary>
    /// Sets the active session. Called by ChatPage when navigation parameters change.
    /// Cancels any existing SSE subscription, clears messages, and loads the new session.
    /// Publishes a <see cref="CurrentSessionChangedMessage"/> so <see cref="FlyoutViewModel"/>
    /// can highlight the active session in the drawer.
    /// </summary>
    /// <remarks>
    /// This method replaces <c>IQueryAttributable.ApplyQueryAttributes</c> to maintain
    /// the zero-MAUI-dependency rule in <c>openMob.Core</c>. The MAUI <c>ChatPage.xaml.cs</c>
    /// implements <c>IQueryAttributable</c> and delegates to this method.
    /// </remarks>
    /// <param name="sessionId">The session ID to load.</param>
    public void SetSession(string sessionId)
    {
        ArgumentNullException.ThrowIfNull(sessionId);

        if (sessionId == CurrentSessionId)
            return;

        // Cancel existing SSE subscription
        _sseCts?.Cancel();
        _sseCts?.Dispose();
        _sseCts = null;

        CurrentSessionId = sessionId;

        // Notify FlyoutViewModel to highlight the newly active session in the drawer
        WeakReferenceMessenger.Default.Send(new CurrentSessionChangedMessage(sessionId));

        // Clear messages immediately to avoid flash of old content (Q3 resolution)
        Messages.Clear();
        UpdateIsEmpty();
        ResetPermissionState();
        ResetQuestionState();

        LoadMessagesCommand.Execute(null);
    }

    // ─── Existing Commands ────────────────────────────────────────────────────

    /// <summary>
    /// Loads the current project and session info and resolves the active server name.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    [RelayCommand]
    private async Task LoadContextAsync(CancellationToken ct)
    {
#if DEBUG
        var sw = Stopwatch.StartNew();
        DebugLogger.LogCommand(nameof(LoadContextAsync), "start");
        try
        {
#endif
        try
        {
            // Load current project from client-side active project state
            var currentProject = await _activeProjectService.GetActiveProjectAsync(ct);
            if (currentProject is not null)
            {
                CurrentProjectId = currentProject.Id;
                ProjectName = ProjectNameHelper.ExtractFromWorktree(currentProject.Worktree);

                // Cache the project directory for SSE event filtering (REQ-004).
                _currentProjectDirectory = _activeProjectService.GetCachedWorktree();

                // Load default model preference for this project
                var pref = await _preferenceService.GetAsync(currentProject.Id, ct);
                if (pref?.DefaultModelId is not null)
                {
                    SelectedModelId = pref.DefaultModelId;
                    SelectedModelName = ModelIdHelper.ExtractModelName(pref.DefaultModelId);
                }

                // Load agent name from preference [REQ-007]
                SelectedAgentName = pref?.AgentName;

                // Load thinking level and auto-accept from preference [REQ-003, REQ-005]
                ThinkingLevel = pref?.ThinkingLevel ?? ThinkingLevel.Medium;
                AutoAccept = pref?.AutoAccept ?? false;

                // Load show-unhandled-SSE-events preference [REQ-008]
                ShowUnhandledSseEvents = pref?.ShowUnhandledSseEvents ?? false;
            }
            else
            {
                CurrentProjectId = null;
                ProjectName = "No project";
                _currentProjectDirectory = null;
            }

            // Load current session if set
            if (CurrentSessionId is not null)
            {
                var session = await _sessionService.GetSessionAsync(CurrentSessionId, ct);
                if (session is not null)
                {
                    SessionName = session.Title;
                }
            }

            // Resolve the active server display name for the connection footer
            ActiveServerName = await _connectionManager.GetActiveServerNameAsync(ct).ConfigureAwait(false) ?? string.Empty;
        }
        catch (Exception ex)
        {
            SentryHelper.CaptureException(ex, new Dictionary<string, object>
            {
                ["context"] = "ChatViewModel.LoadContextAsync",
            });
        }
#if DEBUG
        sw.Stop();
        DebugLogger.LogCommand(nameof(LoadContextAsync), "complete", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            DebugLogger.LogCommand(nameof(LoadContextAsync), "failed", error: $"{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            throw;
        }
#endif
    }

    /// <summary>
    /// Signals intent to open the ProjectSwitcherSheet popup.
    /// The View layer handles popup creation and presentation.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    [RelayCommand]
    private async Task OpenProjectSwitcherAsync(CancellationToken ct)
    {
#if DEBUG
        var sw = Stopwatch.StartNew();
        DebugLogger.LogCommand(nameof(OpenProjectSwitcherAsync), "start");
        try
        {
#endif
        // Delegate to IAppPopupService which resolves and presents the ProjectSwitcherSheet
        // via UXDivers popup stack with pre-loaded project data.
        await _popupService.ShowProjectSwitcherAsync(ct);
#if DEBUG
        sw.Stop();
        DebugLogger.LogCommand(nameof(OpenProjectSwitcherAsync), "complete", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            DebugLogger.LogCommand(nameof(OpenProjectSwitcherAsync), "failed", error: $"{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            throw;
        }
#endif
    }

    /// <summary>
    /// Creates a new session via <see cref="ISessionService"/> and updates the
    /// current session context.
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
            // Note: no ConfigureAwait(false) here — NewChatAsync runs in a ViewModel and must
            // remain on the UI SynchronizationContext so that SetSession, ShowErrorAsync, and
            // any subsequent UI mutations execute on the main thread (required on Android).
            var session = await _sessionService.CreateSessionAsync(null, ct);

            if (session is not null)
            {
                SessionName = session.Title;
                SetSession(session.Id);
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

    /// <summary>
    /// Shows the More Menu option sheet and routes the selected action.
    /// Options: Rename session, Change agent, Change model, Fork session, Archive, Delete.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    [RelayCommand]
    private async Task ShowMoreMenuAsync(CancellationToken ct)
    {
#if DEBUG
        var sw = Stopwatch.StartNew();
        DebugLogger.LogCommand(nameof(ShowMoreMenuAsync), "start");
        try
        {
#endif
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
                // Open the agent picker and update in-memory selection.
                // No persistence to SQLite — this is a session-level override.
                await _popupService.ShowAgentPickerAsync(agentName =>
                {
                    SelectedAgentName = agentName;
                }, ct);
                break;

            case "Change model":
                // Open the model picker and update in-memory selection.
                // No persistence to SQLite — this is a session-level override.
                await _popupService.ShowModelPickerAsync(modelId =>
                {
                    SelectedModelId = modelId;
                    SelectedModelName = ModelIdHelper.ExtractModelName(modelId);
                }, ct);
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
#if DEBUG
        sw.Stop();
        DebugLogger.LogCommand(nameof(ShowMoreMenuAsync), "complete", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            DebugLogger.LogCommand(nameof(ShowMoreMenuAsync), "failed", error: $"{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            throw;
        }
#endif
    }

    // ─── Chat Commands [REQ-006 through REQ-010] ──────────────────────────────

    /// <summary>
    /// Loads the message history for the current session from the server [REQ-006].
    /// On success, populates <see cref="Messages"/> and starts the SSE subscription.
    /// On failure, sets <see cref="ErrorMessage"/> with a user-friendly description.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    [RelayCommand]
    private async Task LoadMessagesAsync(CancellationToken ct)
    {
#if DEBUG
        var sw = Stopwatch.StartNew();
        DebugLogger.LogCommand(nameof(LoadMessagesAsync), "start");
        try
        {
#endif
        if (CurrentSessionId is null)
            return;

        IsBusy = true;
        ErrorMessage = null;

        try
        {
            var result = await _chatService.GetMessagesAsync(CurrentSessionId, ct: ct);

            if (result.IsSuccess && result.Value is not null)
            {
                _dispatcher.Dispatch(() =>
                {
                    Messages.Clear();

                    foreach (var dto in result.Value)
                    {
                        var msg = ChatMessage.FromDto(dto);

                        // Populate non-text parts from the initial HTTP load [REQ-017, REQ-018].
                        // LoadMessagesAsync uses ChatMessage.FromDto which only extracts text parts.
                        // Tool calls, reasoning, step counts, subtask labels, and compaction notices
                        // must be hydrated here from the full DTO parts list.
                        if (dto.Parts is not null)
                        {
                            foreach (var part in dto.Parts)
                            {
                                if (string.Equals(part.Type, "tool", StringComparison.OrdinalIgnoreCase))
                                {
                                    UpsertToolCall(msg, part);
                                }
                                else if (string.Equals(part.Type, "reasoning", StringComparison.OrdinalIgnoreCase) &&
                                         !string.IsNullOrEmpty(part.Text))
                                {
                                    msg.ReasoningText = part.Text;
                                }
                                else if (string.Equals(part.Type, "step-start", StringComparison.OrdinalIgnoreCase))
                                {
                                    msg.StepCount++;
                                }
                                else if (string.Equals(part.Type, "step-finish", StringComparison.OrdinalIgnoreCase))
                                {
                                    if (part.Extras is not null &&
                                        part.Extras.TryGetValue("cost", out var costEl) &&
                                        costEl.ValueKind == System.Text.Json.JsonValueKind.Number)
                                    {
                                        msg.LastStepCost = costEl.GetDecimal();
                                    }
                                }
                                else if (string.Equals(part.Type, "subtask", StringComparison.OrdinalIgnoreCase))
                                {
                                    var agent = part.Extras is not null && part.Extras.TryGetValue("agent", out var agentEl)
                                        ? agentEl.GetString() : null;
                                    var description = part.Extras is not null && part.Extras.TryGetValue("description", out var descEl)
                                        ? descEl.GetString() : null;
                                    var label = (agent, description) switch
                                    {
                                        ({ } a, { } d) => $"{a}: {d}",
                                        ({ } a, null) => a,
                                        (null, { } d) => d,
                                        _ => part.Id,
                                    };
                                    msg.SubtaskLabels.Add(label);
                                }
                                else if (string.Equals(part.Type, "agent", StringComparison.OrdinalIgnoreCase))
                                {
                                    var name = part.Extras is not null && part.Extras.TryGetValue("name", out var nameEl)
                                        ? nameEl.GetString() : null;
                                    if (!string.IsNullOrEmpty(name))
                                        msg.SubtaskLabels.Add(name);
                                }
                                else if (string.Equals(part.Type, "compaction", StringComparison.OrdinalIgnoreCase))
                                {
                                    var isAuto = part.Extras is not null &&
                                                 part.Extras.TryGetValue("auto", out var autoEl) &&
                                                 autoEl.ValueKind == System.Text.Json.JsonValueKind.True;
                                    msg.CompactionNotice = isAuto ? "Context auto-compacted" : "Context compacted";
                                }
                            }
                        }

                        Messages.Add(msg);
                    }

                    RecalculateGrouping();
                    UpdateIsEmpty();
                });

                // Recover any pending questions for this session [REQ-010, REQ-011].
                // Uses GET /question with a 2-second timeout.
                await RecoverPendingQuestionsAsync(ct).ConfigureAwait(false);

                // Start SSE subscription only after messages loaded successfully [REQ-011].
                // Fire-and-forget is safe here: the task is lifecycle-managed via _sseCts
                // (cancelled on session change or Dispose) and all exceptions are caught
                // internally within StartSseSubscriptionAsync.
                _ = StartSseSubscriptionAsync();
            }
            else if (result.Error is not null)
            {
                ErrorMessage = MapChatServiceError(result.Error);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when the operation is cancelled — do not set error state.
        }
        catch (Exception ex)
        {
            ErrorMessage = "An unexpected error occurred while loading messages.";
            SentryHelper.CaptureException(ex, new Dictionary<string, object>
            {
                ["context"] = "ChatViewModel.LoadMessagesAsync",
                ["sessionId"] = CurrentSessionId ?? "null",
            });
        }
        finally
        {
            IsBusy = false;
        }
#if DEBUG
        sw.Stop();
        DebugLogger.LogCommand(nameof(LoadMessagesAsync), "complete", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            DebugLogger.LogCommand(nameof(LoadMessagesAsync), "failed", error: $"{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            throw;
        }
#endif
    }

    /// <summary>
    /// Fetches all pending question requests for the current session and injects
    /// question cards into <see cref="Messages"/> for any that are not already present.
    /// Uses a 2-second timeout to prevent hanging. Called from <see cref="LoadMessagesAsync"/>
    /// and on SSE reconnect transitions [REQ-010, REQ-011, REQ-012].
    /// </summary>
    /// <param name="ct">Caller's cancellation token.</param>
    private async Task RecoverPendingQuestionsAsync(CancellationToken ct)
    {
        var sessionId = CurrentSessionId;
        if (string.IsNullOrEmpty(sessionId))
            return;

        try
        {
            // 2-second timeout linked to the caller's token
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(2));

            var result = await _apiClient.GetPendingQuestionsAsync(timeoutCts.Token)
                .ConfigureAwait(false);

            if (!result.IsSuccess || result.Value is null)
                return;

            var pendingForSession = result.Value
                .Where(q => string.Equals(q.SessionId, sessionId, StringComparison.Ordinal))
                .ToList();

            if (pendingForSession.Count == 0)
                return;

            _dispatcher.Dispatch(() =>
            {
                foreach (var dto in pendingForSession)
                {
                    // Duplicate guard: skip if a card with this ID already exists
                    if (Messages.Any(m => m.MessageKind == MessageKind.QuestionRequest && m.QuestionId == dto.Id))
                        continue;

                    if (dto.Questions is not { Count: > 0 })
                        continue;

                    var firstQ = dto.Questions[0];
                    var questionText = firstQ.Question;
                    if (string.IsNullOrEmpty(questionText))
                        continue;

                    var options = firstQ.Options?.Select(o => o.Label).ToList()
                        ?? (IReadOnlyList<string>)Array.Empty<string>();
                    var allowFreeText = firstQ.Custom ?? true;

                    var card = ChatMessage.CreateQuestionRequest(
                        dto.Id,
                        sessionId,
                        questionText,
                        options,
                        allowFreeText);

                    Messages.Add(card);

                    // REQ-016: Retroactively hide tool call cards that match this question
                    var toolCallId = dto.Tool?.CallId;
                    if (!string.IsNullOrEmpty(toolCallId))
                        HideToolCallByCallId(toolCallId);

                    IncrementPendingQuestions();
                }

                RecalculateGrouping();
                UpdateIsEmpty();
            });
        }
        catch (OperationCanceledException)
        {
            // Timeout or cancellation — expected, do not surface as error
        }
        catch (Exception ex)
        {
            SentryHelper.CaptureException(ex, new Dictionary<string, object>
            {
                ["context"] = "ChatViewModel.RecoverPendingQuestionsAsync",
                ["sessionId"] = sessionId,
            });
        }
    }

    /// <summary>
    /// Sends a user message to the current session with optimistic UI [REQ-007].
    /// The message is added to <see cref="Messages"/> immediately with
    /// <see cref="MessageDeliveryStatus.Sending"/>, then confirmed or marked as error
    /// based on the server response.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    [RelayCommand(CanExecute = nameof(CanSendMessage))]
    private async Task SendMessageAsync(CancellationToken ct)
    {
#if DEBUG
        var sw = Stopwatch.StartNew();
        DebugLogger.LogCommand(nameof(SendMessageAsync), "start");
        try
        {
#endif
        if (CurrentSessionId is null)
            return;

        // Capture text and clear input immediately
        var text = InputText;
        InputText = string.Empty;

        // Create optimistic message
        var optimisticMessage = ChatMessage.CreateOptimistic(CurrentSessionId, text);

        _dispatcher.Dispatch(() =>
        {
            Messages.Add(optimisticMessage);
            RecalculateGrouping();
            UpdateIsEmpty();
        });

        // Extract providerId/modelId from SelectedModelId
        string? providerId = null;
        string? modelId = null;

        if (SelectedModelId is not null)
        {
            var parts = SelectedModelId.Split('/', 2);
            providerId = parts[0];
            modelId = parts.Length > 1 ? parts[1] : null;
        }

        IsAiResponding = true;

        try
        {
            var result = await _chatService.SendPromptAsync(
                CurrentSessionId,
                text,
                modelId,
                providerId,
                SelectedAgentName,
                ct);

            if (result.IsSuccess)
            {
                optimisticMessage.DeliveryStatus = MessageDeliveryStatus.Sent;
            }
            else if (result.Error is not null)
            {
                optimisticMessage.DeliveryStatus = MessageDeliveryStatus.Error;
                ErrorMessage = MapChatServiceError(result.Error);
                IsAiResponding = false;
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when the operation is cancelled.
            optimisticMessage.DeliveryStatus = MessageDeliveryStatus.Error;
            IsAiResponding = false;
        }
        catch (Exception ex)
        {
            optimisticMessage.DeliveryStatus = MessageDeliveryStatus.Error;
            ErrorMessage = "Failed to send message. Please try again.";
            IsAiResponding = false;
            SentryHelper.CaptureException(ex, new Dictionary<string, object>
            {
                ["context"] = "ChatViewModel.SendMessageAsync",
                ["sessionId"] = CurrentSessionId ?? "null",
            });
        }
#if DEBUG
        sw.Stop();
        DebugLogger.LogCommand(nameof(SendMessageAsync), "complete", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            DebugLogger.LogCommand(nameof(SendMessageAsync), "failed", error: $"{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            throw;
        }
#endif
    }

    /// <summary>Determines whether <see cref="SendMessageCommand"/> can execute.</summary>
    /// <returns><c>true</c> if input text is non-empty and the AI is not currently responding.</returns>
    private bool CanSendMessage() => !string.IsNullOrWhiteSpace(InputText) && !IsAiResponding;

    /// <summary>Notifies the message composer when the AI streaming state changes.</summary>
    /// <param name="value">The new streaming state.</param>
    partial void OnIsAiRespondingChanged(bool value)
    {
        WeakReferenceMessenger.Default.Send(new StreamingStateChangedMessage(value));
    }

    // ─── Message Composer [REQ-004, REQ-023, REQ-024] ─────────────────────────

    /// <summary>
    /// Opens the message composer popup for the current project and session [REQ-004].
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    [RelayCommand]
    private async Task OpenMessageComposerAsync(CancellationToken ct)
    {
        if (CurrentProjectId is null || CurrentSessionId is null)
            return;

        await _popupService.ShowMessageComposerAsync(CurrentProjectId, CurrentSessionId, IsAiResponding, SelectedModelId, ct);
    }

    /// <summary>
    /// Handles a composed message from <see cref="MessageComposerViewModel"/> [REQ-024].
    /// Uses the override values for this single send only — does not mutate persistent state.
    /// </summary>
    /// <param name="message">The composed message with text and session overrides.</param>
    private async Task HandleMessageComposedAsync(MessageComposedMessage message)
    {
        if (message.SessionId != CurrentSessionId || CurrentProjectId is null)
            return;

        // Apply and persist all overrides from the composer as new project defaults
        if (message.AgentOverride is not null && message.AgentOverride != SelectedAgentName)
        {
            SelectedAgentName = message.AgentOverride;
            _ = _preferenceService.SetAgentAsync(CurrentProjectId, message.AgentOverride);
        }

        if (message.ModelIdOverride is not null && message.ModelIdOverride != SelectedModelId)
        {
            SelectedModelId = message.ModelIdOverride;
            SelectedModelName = Helpers.ModelIdHelper.ExtractModelName(message.ModelIdOverride);
            _ = _preferenceService.SetDefaultModelAsync(CurrentProjectId, message.ModelIdOverride);
        }

        if (message.ThinkingLevelOverride != ThinkingLevel)
        {
            ThinkingLevel = message.ThinkingLevelOverride;
            _ = _preferenceService.SetThinkingLevelAsync(CurrentProjectId, message.ThinkingLevelOverride);
        }

        if (message.AutoAcceptOverride != AutoAccept)
        {
            AutoAccept = message.AutoAcceptOverride;
            _ = _preferenceService.SetAutoAcceptAsync(CurrentProjectId, message.AutoAcceptOverride);
        }

        // The primary agent is a project-level preference (persisted above) — it is NOT
        // sent as @mention in the text. Only subagents (inserted via the @ toolbar button
        // in the composer) appear as @mentions in the message text.
        var text = message.Text;

        // Use InputText + SendMessageCommand to leverage existing optimistic UI logic
        InputText = text;
        if (SendMessageCommand.CanExecute(null))
        {
            await SendMessageCommand.ExecuteAsync(null);
        }
    }

    /// <summary>
    /// Cancels the AI response currently in progress [REQ-008].
    /// Calls <see cref="IOpencodeApiClient.AbortSessionAsync"/> and sets
    /// <see cref="IsAiResponding"/> to <c>false</c>. Partial messages already
    /// received via SSE are preserved.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    [RelayCommand(CanExecute = nameof(IsAiResponding))]
    private async Task CancelResponseAsync(CancellationToken ct)
    {
#if DEBUG
        var sw = Stopwatch.StartNew();
        DebugLogger.LogCommand(nameof(CancelResponseAsync), "start");
        try
        {
#endif
        if (CurrentSessionId is null)
            return;

        try
        {
            await _apiClient.AbortSessionAsync(CurrentSessionId, ct);
        }
        catch (Exception ex)
        {
            SentryHelper.CaptureException(ex, new Dictionary<string, object>
            {
                ["context"] = "ChatViewModel.CancelResponseAsync",
                ["sessionId"] = CurrentSessionId,
            });
        }
        finally
        {
            IsAiResponding = false;
        }
#if DEBUG
        sw.Stop();
        DebugLogger.LogCommand(nameof(CancelResponseAsync), "complete", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            DebugLogger.LogCommand(nameof(CancelResponseAsync), "failed", error: $"{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            throw;
        }
#endif
    }

    /// <summary>
    /// Handles a suggestion chip tap [REQ-009]. Sets <see cref="InputText"/> to the
    /// chip's prompt text and invokes <see cref="SendMessageCommand"/>.
    /// </summary>
    /// <param name="chip">The selected suggestion chip.</param>
    [RelayCommand]
    private async Task SelectSuggestionChipAsync(SuggestionChip chip)
    {
#if DEBUG
        var sw = Stopwatch.StartNew();
        DebugLogger.LogCommand(nameof(SelectSuggestionChipAsync), "start");
        try
        {
#endif
        ArgumentNullException.ThrowIfNull(chip);

        InputText = chip.PromptText;
        await SendMessageCommand.ExecuteAsync(null);
#if DEBUG
        sw.Stop();
        DebugLogger.LogCommand(nameof(SelectSuggestionChipAsync), "complete", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            DebugLogger.LogCommand(nameof(SelectSuggestionChipAsync), "failed", error: $"{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            throw;
        }
#endif
    }

    /// <summary>
    /// Dismisses the current error message [REQ-010].
    /// </summary>
    [RelayCommand]
    private void DismissError()
    {
#if DEBUG
        DebugLogger.LogCommand(nameof(DismissError), "start");
#endif
        ErrorMessage = null;
#if DEBUG
        DebugLogger.LogCommand(nameof(DismissError), "complete");
#endif
    }

    // ─── Chat Page Redesign Commands [REQ-023, REQ-025, REQ-029, REQ-022] ────

    /// <summary>
    /// Renames the current session (REQ-023). Opens a rename dialog pre-filled
    /// with the current session name, then updates via the session service.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    [RelayCommand]
    private async Task RenameSessionAsync(CancellationToken ct)
    {
#if DEBUG
        var sw = Stopwatch.StartNew();
        DebugLogger.LogCommand(nameof(RenameSessionAsync), "start");
        try
        {
#endif
        await HandleRenameSessionAsync(ct);
#if DEBUG
        sw.Stop();
        DebugLogger.LogCommand(nameof(RenameSessionAsync), "complete", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            DebugLogger.LogCommand(nameof(RenameSessionAsync), "failed", error: $"{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            throw;
        }
#endif
    }

    /// <summary>
    /// Opens the Context Sheet bottom sheet for the current project (REQ-025, REQ-012).
    /// Passes the current project and session identifiers so the sheet can load
    /// the correct preferences.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    [RelayCommand]
    private async Task OpenContextSheetAsync(CancellationToken ct)
    {
#if DEBUG
        var sw = Stopwatch.StartNew();
        DebugLogger.LogCommand(nameof(OpenContextSheetAsync), "start");
        try
        {
#endif
        if (CurrentProjectId is null)
            return;

        await _popupService.ShowContextSheetAsync(
            CurrentProjectId,
            CurrentSessionId ?? string.Empty,
            ct);
#if DEBUG
        sw.Stop();
        DebugLogger.LogCommand(nameof(OpenContextSheetAsync), "complete", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            DebugLogger.LogCommand(nameof(OpenContextSheetAsync), "failed", error: $"{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            throw;
        }
#endif
    }

    /// <summary>
    /// Opens the Command Palette bottom sheet (REQ-029).
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    [RelayCommand]
    private async Task OpenCommandPaletteAsync(CancellationToken ct)
    {
#if DEBUG
        var sw = Stopwatch.StartNew();
        DebugLogger.LogCommand(nameof(OpenCommandPaletteAsync), "start");
        try
        {
#endif
        await _popupService.ShowCommandPaletteAsync(ct);
#if DEBUG
        sw.Stop();
        DebugLogger.LogCommand(nameof(OpenCommandPaletteAsync), "complete", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            DebugLogger.LogCommand(nameof(OpenCommandPaletteAsync), "failed", error: $"{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            throw;
        }
#endif
    }

    /// <summary>
    /// Handles scroll direction changes to show/hide the context status bar (REQ-022).
    /// When scrolling down and messages exist, the bar collapses.
    /// When scrolling up or at the top, the bar reappears.
    /// When the message list is empty, the bar always remains visible (REQ-037).
    /// </summary>
    /// <param name="isScrollingDown"><c>true</c> if the user is scrolling down; <c>false</c> if scrolling up or at top.</param>
    public void OnScrollDirectionChanged(bool isScrollingDown)
    {
        if (IsEmpty)
        {
            IsContextBarVisible = true;
            return;
        }

        IsContextBarVisible = !isScrollingDown;
    }

    // ─── SSE Subscription [REQ-011] ───────────────────────────────────────────

    /// <summary>
    /// Starts the SSE event subscription for the current session.
    /// Runs on a background task and processes events until cancelled or an error occurs.
    /// </summary>
    private async Task StartSseSubscriptionAsync()
    {
        // Cancel any previous subscription
        _sseCts?.Cancel();
        _sseCts?.Dispose();
        _sseCts = new CancellationTokenSource();
        var ct = _sseCts.Token;

        try
        {
            await foreach (var chatEvent in _chatService.SubscribeToEventsAsync(ct))
            {
                switch (chatEvent)
                {
                    case MessageUpdatedEvent e:
                        HandleMessageUpdated(e);
                        break;

                    case MessagePartDeltaEvent e:
                        HandleMessagePartDelta(e);
                        break;

                    case MessagePartUpdatedEvent e:
                        HandleMessagePartUpdated(e);
                        break;

                    case SessionUpdatedEvent e:
                        HandleSessionUpdated(e);
                        break;

                    case SessionErrorEvent e:
                        HandleSessionError(e);
                        break;

                    case PermissionRequestedEvent e:
                        HandlePermissionRequested(e);
                        break;

                    case PermissionRepliedEvent e:
                        HandlePermissionReplied(e);
                        break;

                    case QuestionRequestedEvent e:
                        HandleQuestionRequested(e);
                        break;

                    case MessageRemovedEvent e:
                        HandleMessageRemoved(e);
                        break;

                    case MessagePartRemovedEvent e:
                        HandleMessagePartRemoved(e);
                        break;

                    case SessionCreatedEvent e:
                        HandleSessionCreated(e);
                        break;

                    case SessionDeletedEvent e:
                        HandleSessionDeleted(e);
                        break;

                    case UnknownEvent e when string.Equals(e.RawType, "server.heartbeat", StringComparison.OrdinalIgnoreCase):
                        OnHeartbeatReceived();
                        break;

                    case UnknownEvent e:
#if DEBUG
                        DebugLogger.WriteAction("OM_SSE", $"[SSE] Unknown event type: '{e.RawType}'");
#endif
                        HandleUnknownEvent(e);
                        break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when the subscription is cancelled (session change, dispose).
        }
        catch (Exception ex)
        {
            ErrorMessage = "Lost connection to the server. Messages may be delayed.";
            SentryHelper.CaptureException(ex, new Dictionary<string, object>
            {
                ["context"] = "ChatViewModel.StartSseSubscriptionAsync",
                ["sessionId"] = CurrentSessionId ?? "null",
            });
        }
    }

    // ─── SSE Event Handlers [REQ-012 through REQ-015] ─────────────────────────

    /// <summary>
    /// Handles a <see cref="MessageUpdatedEvent"/> from the SSE stream [REQ-012].
    /// Updates an existing message or adds a new one to the collection.
    /// </summary>
    /// <param name="e">The message updated event.</param>
    private void HandleMessageUpdated(MessageUpdatedEvent e)
    {
        // Filter by project directory first (REQ-003, REQ-005, REQ-006).
        if (e.ProjectDirectory is not null &&
            e.ProjectDirectory != _currentProjectDirectory)
            return;

        if (e.Message.Info.SessionId != CurrentSessionId)
            return;

        _dispatcher.Dispatch(() =>
        {
            var existing = FindMessageById(e.Message.Info.Id);

#if DEBUG
            System.Diagnostics.Debug.WriteLine($"[MSG_UPDATED] event id='{e.Message.Info.Id}' role='{e.Message.Info.Role}' sessionId='{e.Message.Info.SessionId}' currentSessionId='{CurrentSessionId}' existingFound={existing is not null}");
#endif

            if (existing is not null)
            {
                // Only overwrite TextContent if the event carries actual text parts.
                // When Parts is null or empty (intermediate streaming events), preserve
                // the text already accumulated via message.part.delta events.
                var extractedText = ChatMessage.ExtractTextContent(e.Message.Parts ?? []);
                if (!string.IsNullOrEmpty(extractedText))
                {
                    existing.TextContent = extractedText;
                }
                existing.IsStreaming = !existing.IsFromUser && !ChatMessage.HasCompletedTimestamp(e.Message.Info.Time);
                existing.DeliveryStatus = MessageDeliveryStatus.Sent;

                // Upsert tool calls from the full message parts [REQ-018]
                if (e.Message.Parts is not null)
                {
                    foreach (var part in e.Message.Parts)
                    {
                        if (string.Equals(part.Type, "tool", StringComparison.OrdinalIgnoreCase))
                        {
                            UpsertToolCall(existing, part);
                        }
                    }
                }
            }
            else
            {
                var newMessage = ChatMessage.FromDto(e.Message);

#if DEBUG
                System.Diagnostics.Debug.WriteLine($"[MSG_UPDATED] no existing match for id='{e.Message.Info.Id}' role='{e.Message.Info.Role}' isFromUser={newMessage.IsFromUser} TextContent='{newMessage.TextContent}' PartsCount={e.Message.Parts?.Count ?? -1}");
                if (e.Message.Parts != null)
                {
                    foreach (var p in e.Message.Parts)
                        System.Diagnostics.Debug.WriteLine($"[MSG_UPDATED]   part id='{p.Id}' type='{p.Type}' text='{p.Text}' extras={p.Extras?.Count ?? 0}");
                }
                System.Diagnostics.Debug.WriteLine($"[MSG_UPDATED] Messages before reconciliation: {Messages.Count} items");
                for (var dbgI = 0; dbgI < Messages.Count; dbgI++)
                    System.Diagnostics.Debug.WriteLine($"[MSG_UPDATED]   [{dbgI}] id='{Messages[dbgI].Id}' isFromUser={Messages[dbgI].IsFromUser} text='{Messages[dbgI].TextContent}' status={Messages[dbgI].DeliveryStatus}");
#endif

                // Optimistic reconciliation: if this is a user message, find and replace
                // the optimistic placeholder (which has a temporary GUID id) that matches
                // by text content. This prevents the message from appearing twice.
                if (newMessage.IsFromUser)
                {
                    var optimisticIndex = FindOptimisticUserMessageIndex();
#if DEBUG
                    System.Diagnostics.Debug.WriteLine($"[MSG_UPDATED] optimisticIndex={optimisticIndex} (IsOptimistic-based search)");
#endif
                    if (optimisticIndex >= 0)
                    {
                        Messages[optimisticIndex] = newMessage;
                        existing = newMessage;
                    }
                    else
                    {
                        Messages.Add(newMessage);
                        existing = newMessage;
                    }
                }
                else
                {
                    Messages.Add(newMessage);
                    existing = newMessage;
                }

                // Upsert tool calls from the full message parts for the new message [REQ-018]
                if (e.Message.Parts is not null)
                {
                    foreach (var part in e.Message.Parts)
                    {
                        if (string.Equals(part.Type, "tool", StringComparison.OrdinalIgnoreCase))
                        {
                            UpsertToolCall(existing, part);
                        }
                    }
                }
            }

            RecalculateGrouping();

            // If this is a completed assistant message, stop the responding indicator
            if (!existing.IsFromUser && !existing.IsStreaming)
            {
                IsAiResponding = false;
            }

            // Subagent detection: if the message is from a subagent sender,
            // track the active subagent state. When the message completes
            // (has a completed timestamp), clear the subagent indicator.
            if (existing.SenderType == SenderType.Subagent)
            {
                if (existing.IsStreaming)
                {
                    IsSubagentActive = true;
                    SubagentName = existing.SenderName;
                }
                else
                {
                    IsSubagentActive = false;
                }
            }
        });
    }

    /// <summary>
    /// Handles a <see cref="MessagePartUpdatedEvent"/> from the SSE stream [REQ-013].
    /// Updates the text content of an existing message with the new part data.
    /// </summary>
    /// <param name="e">The message part updated event.</param>
    private void HandleMessagePartUpdated(MessagePartUpdatedEvent e)
    {
        // Filter by project directory first (REQ-003, REQ-005, REQ-006).
        if (e.ProjectDirectory is not null &&
            e.ProjectDirectory != _currentProjectDirectory)
            return;

        if (e.Part.SessionId != CurrentSessionId)
            return;

        _dispatcher.Dispatch(() =>
        {
            var existing = FindMessageById(e.Part.MessageId);

            if (existing is not null)
            {
                if (string.Equals(e.Part.Type, "text", StringComparison.OrdinalIgnoreCase))
                {
                    // The opencode server returns text directly in the "text" field of the part DTO.
                    if (!string.IsNullOrEmpty(e.Part.Text))
                    {
                        existing.TextContent = e.Part.Text;
                    }

                    existing.IsStreaming = true;
                }
                else if (string.Equals(e.Part.Type, "tool", StringComparison.OrdinalIgnoreCase))
                {
                    UpsertToolCall(existing, e.Part);

                    // REQ-014: Suppress tool call card when toolName is "question" and a question card exists
                    if (string.Equals(e.Part.ToolName, "question", StringComparison.OrdinalIgnoreCase))
                    {
                        var toolCallId = e.Part.CallId;
                        if (!string.IsNullOrEmpty(toolCallId))
                        {
                            // Check if a question card already exists for this tool call
                            if (Messages.Any(m => m.MessageKind == MessageKind.QuestionRequest))
                            {
                                // Find the ToolCallInfo we just upserted and hide it
                                var toolCall = existing.ToolCalls?.FirstOrDefault(tc => tc.PartId == e.Part.Id);
                                if (toolCall is not null)
                                    toolCall.IsHidden = true;
                            }
                        }
                    }
                }
                else if (string.Equals(e.Part.Type, "reasoning", StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.IsNullOrEmpty(e.Part.Text))
                        existing.ReasoningText = e.Part.Text;
                }
                else if (string.Equals(e.Part.Type, "step-start", StringComparison.OrdinalIgnoreCase))
                {
                    existing.StepCount++;
                }
                else if (string.Equals(e.Part.Type, "step-finish", StringComparison.OrdinalIgnoreCase))
                {
                    // cost is in Extras["cost"] as a JsonElement
                    if (e.Part.Extras is not null &&
                        e.Part.Extras.TryGetValue("cost", out var costEl) &&
                        costEl.ValueKind == JsonValueKind.Number)
                    {
                        existing.LastStepCost = costEl.GetDecimal();
                    }
                }
                else if (string.Equals(e.Part.Type, "subtask", StringComparison.OrdinalIgnoreCase))
                {
                    var agent = e.Part.Extras is not null && e.Part.Extras.TryGetValue("agent", out var agentEl)
                        ? agentEl.GetString() : null;
                    var description = e.Part.Extras is not null && e.Part.Extras.TryGetValue("description", out var descEl)
                        ? descEl.GetString() : null;
                    var label = (agent, description) switch
                    {
                        ({ } a, { } d) => $"{a}: {d}",
                        ({ } a, null) => a,
                        (null, { } d) => d,
                        _ => e.Part.Id,
                    };
                    existing.SubtaskLabels.Add(label);
                }
                else if (string.Equals(e.Part.Type, "agent", StringComparison.OrdinalIgnoreCase))
                {
                    var name = e.Part.Extras is not null && e.Part.Extras.TryGetValue("name", out var nameEl)
                        ? nameEl.GetString() : null;
                    if (!string.IsNullOrEmpty(name))
                        existing.SubtaskLabels.Add(name);
                }
                else if (string.Equals(e.Part.Type, "compaction", StringComparison.OrdinalIgnoreCase))
                {
                    var isAuto = e.Part.Extras is not null &&
                                 e.Part.Extras.TryGetValue("auto", out var autoEl) &&
                                 autoEl.ValueKind == JsonValueKind.True;
                    existing.CompactionNotice = isAuto ? "Context auto-compacted" : "Context compacted";
                }
                else if (string.Equals(e.Part.Type, "snapshot", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(e.Part.Type, "patch", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(e.Part.Type, "retry", StringComparison.OrdinalIgnoreCase))
                {
#if DEBUG
                    DebugLogger.WriteAction("OM_SSE", $"[SSE] Ignored part type: {e.Part.Type} (partId={e.Part.Id})");
#endif
                }
            }
        });
    }

    /// <summary>
    /// Handles a <see cref="MessagePartDeltaEvent"/> from the SSE stream.
    /// Appends an incremental text delta to the existing message's text content.
    /// This is the primary real-time streaming event — each delta is a small chunk of text.
    /// </summary>
    /// <param name="e">The message part delta event.</param>
    private void HandleMessagePartDelta(MessagePartDeltaEvent e)
    {
        // Filter by project directory first (REQ-003, REQ-005, REQ-006).
        if (e.ProjectDirectory is not null &&
            e.ProjectDirectory != _currentProjectDirectory)
            return;

        if (e.SessionId != CurrentSessionId)
            return;

        var isText = string.Equals(e.Field, "text", StringComparison.OrdinalIgnoreCase);
        var isReasoning = string.Equals(e.Field, "reasoning", StringComparison.OrdinalIgnoreCase);

        if (!isText && !isReasoning)
            return;

        _dispatcher.Dispatch(() =>
        {
            var existing = FindMessageById(e.MessageId);

            if (existing is not null)
            {
                if (isText)
                {
                    existing.TextContent += e.Delta;
                    existing.IsStreaming = true;
                }
                else // isReasoning
                {
                    existing.ReasoningText += e.Delta;
                    existing.IsStreaming = true;
                }
            }
            else
            {
                // Message not yet in collection — create a placeholder assistant message
                var placeholder = new ChatMessage(
                    id: e.MessageId,
                    sessionId: e.SessionId,
                    isFromUser: false,
                    textContent: isText ? e.Delta : string.Empty,
                    timestamp: DateTimeOffset.UtcNow,
                    deliveryStatus: MessageDeliveryStatus.Sent,
                    isStreaming: true);
                if (isReasoning)
                    placeholder.ReasoningText = e.Delta;
                Messages.Add(placeholder);
                RecalculateGrouping();
                UpdateIsEmpty();
            }
        });
    }

    /// <summary>
    /// Handles a <see cref="SessionUpdatedEvent"/> from the SSE stream [REQ-014].
    /// Updates the session title when the server renames the session and notifies
    /// <see cref="FlyoutViewModel"/> to update the drawer list without a full reload.
    /// </summary>
    /// <param name="e">The session updated event.</param>
    private void HandleSessionUpdated(SessionUpdatedEvent e)
    {
        // Filter by project directory first (REQ-003, REQ-005, REQ-006).
        if (e.ProjectDirectory is not null &&
            e.ProjectDirectory != _currentProjectDirectory)
            return;

        if (e.Session.Id != CurrentSessionId)
            return;

        _dispatcher.Dispatch(() =>
        {
            SessionName = e.Session.Title;
        });

        // Notify FlyoutViewModel to update the session title in the drawer list
        WeakReferenceMessenger.Default.Send(new SessionTitleUpdatedMessage(e.Session.Id, e.Session.Title));
    }

    /// <summary>
    /// Handles a <see cref="SessionErrorEvent"/> from the SSE stream [REQ-015].
    /// Sets the error state and marks the last user message as failed.
    /// </summary>
    /// <param name="e">The session error event.</param>
    private void HandleSessionError(SessionErrorEvent e)
    {
        // Filter by project directory first (REQ-003, REQ-005, REQ-006).
        if (e.ProjectDirectory is not null &&
            e.ProjectDirectory != _currentProjectDirectory)
            return;

        if (e.SessionId != CurrentSessionId)
            return;

        _dispatcher.Dispatch(() =>
        {
            IsAiResponding = false;
            ErrorMessage = e.ErrorMessage;

            // Mark the last user message as error
            for (var i = Messages.Count - 1; i >= 0; i--)
            {
                if (Messages[i].IsFromUser)
                {
                    Messages[i].DeliveryStatus = MessageDeliveryStatus.Error;
                    break;
                }
            }
        });
    }

    /// <summary>
    /// Handles a <see cref="PermissionRequestedEvent"/> from the SSE stream.
    /// Injects an inline permission card into the message list, or auto-replies
    /// with "always" when <see cref="AutoAccept"/> is enabled [REQ-001 through REQ-008].
    /// </summary>
    /// <param name="e">
    /// The permission request event. <see cref="PermissionRequestedEvent.Id"/> is used
    /// as the permission request identifier for the <c>_inFlightPermissionReplies</c>
    /// duplicate guard and the <see cref="IOpencodeApiClient.ReplyToPermissionAsync"/> call.
    /// </param>
    private void HandlePermissionRequested(PermissionRequestedEvent e)
    {
        if (e.ProjectDirectory is not null &&
            e.ProjectDirectory != _currentProjectDirectory)
            return;

        // Auto-accept path [REQ-001 through REQ-008]
        if (AutoAccept)
        {
            var requestId = e.Id;

            // [REQ-008] Duplicate guard — same pattern as ReplyToPermissionAsync
            if (!_inFlightPermissionReplies.Add(requestId))
                return;

            _ = Task.Run(async () =>
            {
                try
                {
                    await _apiClient.ReplyToPermissionAsync(requestId, "always", CancellationToken.None)
                        .ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    SentryHelper.CaptureException(ex, new Dictionary<string, object>
                    {
                        ["context"] = "ChatViewModel.HandlePermissionRequested.AutoAccept",
                        ["requestId"] = requestId,
                        ["sessionId"] = CurrentSessionId ?? "null",
                    });
                }
                finally
                {
                    _inFlightPermissionReplies.Remove(requestId);
                }
            });
            return; // [REQ-003] do not fall through to card rendering
        }

        _dispatcher.Dispatch(() =>
        {
            var message = ChatMessage.CreatePermissionRequest(
                e.Id,
                e.SessionId,
                e.Permission,
                e.Patterns);

            Messages.Add(message);
            RecalculateGrouping();
            UpdateIsEmpty();
            IncrementPendingPermissions();
        });
    }

    /// <summary>
    /// Handles a <see cref="PermissionRepliedEvent"/> from the SSE stream [REQ-003, REQ-004, REQ-005].
    /// Auto-resolves permission cards when the server replies (e.g. auto-approved by rule or rejected in cascade).
    /// </summary>
    private void HandlePermissionReplied(PermissionRepliedEvent e)
    {
        if (e.ProjectDirectory is not null &&
            e.ProjectDirectory != _currentProjectDirectory)
            return;

        if (e.SessionId != CurrentSessionId)
            return;

        var replyLabel = e.Reply switch
        {
            "always" => "Always",
            "once" => "Once",
            "reject" => "Deny",
            _ => e.Reply,
        };

        _dispatcher.Dispatch(() =>
        {
            if (string.Equals(e.Reply, "reject", StringComparison.OrdinalIgnoreCase))
            {
                // Reject cascades to all pending permissions in the session
                for (var i = 0; i < Messages.Count; i++)
                {
                    var msg = Messages[i];
                    if (msg.MessageKind == MessageKind.PermissionRequest &&
                        msg.PermissionStatus == PermissionStatus.Pending)
                    {
                        msg.PermissionStatus = PermissionStatus.Resolved;
                        msg.ResolvedReply = e.Reply;
                        msg.ResolvedReplyLabel = replyLabel;
                        DecrementPendingPermissions();
                    }
                }
            }
            else
            {
                ResolvePermissionRequest(e.RequestId, e.Reply, replyLabel);
            }
        });
    }

    /// <summary>
    /// Handles a <see cref="QuestionRequestedEvent"/> from the SSE stream [REQ-012].
    /// Injects an inline question card into the message list.
    /// </summary>
    /// <param name="e">The question requested event.</param>
    private void HandleQuestionRequested(QuestionRequestedEvent e)
    {
        if (e.ProjectDirectory is not null &&
            e.ProjectDirectory != _currentProjectDirectory)
            return;

        if (e.SessionId != CurrentSessionId)
            return;

        _dispatcher.Dispatch(() =>
        {
            // Duplicate guard: if a card with this question ID already exists, ignore [REQ-012]
            if (Messages.Any(m => m.MessageKind == MessageKind.QuestionRequest && m.QuestionId == e.Id))
                return;

            var message = ChatMessage.CreateQuestionRequest(
                e.Id,
                e.SessionId,
                e.Question,
                e.Options,
                e.AllowFreeText);

            Messages.Add(message);
            RecalculateGrouping();
            UpdateIsEmpty();
            IncrementPendingQuestions();

            // REQ-016: Retroactively hide tool call cards for this question
            if (!string.IsNullOrEmpty(e.ToolCallId))
                HideToolCallByCallId(e.ToolCallId);
        });
    }

    /// <summary>
    /// Handles a <see cref="MessageRemovedEvent"/> from the SSE stream [REQ-007].
    /// Removes the corresponding message from the collection.
    /// </summary>
    private void HandleMessageRemoved(MessageRemovedEvent e)
    {
        if (e.ProjectDirectory is not null &&
            e.ProjectDirectory != _currentProjectDirectory)
            return;

        if (e.SessionId != CurrentSessionId)
            return;

        _dispatcher.Dispatch(() =>
        {
            for (var i = 0; i < Messages.Count; i++)
            {
                if (Messages[i].Id == e.MessageId)
                {
                    Messages.RemoveAt(i);
                    RecalculateGrouping();
                    UpdateIsEmpty();
                    return;
                }
            }
        });
    }

    /// <summary>
    /// Handles a <see cref="MessagePartRemovedEvent"/> from the SSE stream [REQ-009].
    /// Removes the specified part (tool call or reasoning) from the target message.
    /// </summary>
    private void HandleMessagePartRemoved(MessagePartRemovedEvent e)
    {
        if (e.ProjectDirectory is not null &&
            e.ProjectDirectory != _currentProjectDirectory)
            return;

        if (e.SessionId != CurrentSessionId)
            return;

        _dispatcher.Dispatch(() =>
        {
            var existing = FindMessageById(e.MessageId);
            if (existing is null)
                return;

            // Try to remove from ToolCalls first
            for (var i = 0; i < existing.ToolCalls.Count; i++)
            {
                if (existing.ToolCalls[i].PartId == e.PartId)
                {
                    existing.ToolCalls.RemoveAt(i);
                    return;
                }
            }

            // Fallback: if no ToolCallInfo matched, the removed part was likely a reasoning part.
            // Clear the reasoning text if it is non-empty.
            if (!string.IsNullOrEmpty(existing.ReasoningText))
            {
                existing.ReasoningText = string.Empty;
            }
        });
    }

    /// <summary>
    /// Handles a <see cref="SessionCreatedEvent"/> from the SSE stream [REQ-012].
    /// Publishes a <see cref="SessionCreatedMessage"/> so FlyoutViewModel can prepend the session.
    /// No session-ID or project-directory filter — this event is global.
    /// </summary>
    private void HandleSessionCreated(SessionCreatedEvent e)
    {
        WeakReferenceMessenger.Default.Send(new SessionCreatedMessage(
            e.Session.Id,
            e.Session.ProjectId,
            string.IsNullOrEmpty(e.Session.Title) ? "New Session" : e.Session.Title,
            DateTimeOffset.FromUnixTimeMilliseconds(e.Session.Time.Updated)));
    }

    /// <summary>
    /// Handles a <see cref="SessionDeletedEvent"/> from the SSE stream [REQ-013].
    /// Publishes a <see cref="SessionDeletedMessage"/> so FlyoutViewModel can remove the session.
    /// No session-ID or project-directory filter — this event is global.
    /// </summary>
    private void HandleSessionDeleted(SessionDeletedEvent e)
    {
        WeakReferenceMessenger.Default.Send(new SessionDeletedMessage(e.SessionId, e.ProjectId));
    }

    /// <summary>
    /// Handles an <see cref="UnknownEvent"/> from the SSE stream [REQ-001, REQ-004, REQ-005].
    /// When <see cref="ShowUnhandledSseEvents"/> is <c>false</c> (the default), the card is
    /// silently suppressed and no entry is added to <see cref="Messages"/>.
    /// When <c>true</c>, creates a fallback <see cref="ChatMessage"/> and appends it to
    /// <see cref="Messages"/> on the UI thread. In DEBUG builds the raw event type and JSON
    /// payload are preserved; in Release builds both fields are null.
    /// </summary>
    /// <param name="e">The unknown event.</param>
    private void HandleUnknownEvent(UnknownEvent e)
    {
        if (!ShowUnhandledSseEvents)
            return;

#if DEBUG
        string? rawJson = null;
        if (e.RawData.HasValue && e.RawData.Value.ValueKind != JsonValueKind.Undefined)
        {
            rawJson = JsonSerializer.Serialize(e.RawData.Value, new JsonSerializerOptions { WriteIndented = true });
        }
        var fallback = ChatMessage.CreateFallback(e.RawType, rawJson);
#else
        var fallback = ChatMessage.CreateFallback(e.RawType, null);
#endif

        _dispatcher.Dispatch(() =>
        {
            Messages.Add(fallback);
            RecalculateGrouping();
        });
    }

    /// <summary>
    /// Replies to a permission request and resolves the matching message on success.
    /// Concurrent calls for the same <paramref name="requestId"/> are silently dropped
    /// to prevent duplicate API calls on rapid double-tap.
    /// </summary>
    /// <param name="requestId">The permission request identifier.</param>
    /// <param name="reply">The reply value: <c>always</c>, <c>once</c>, or <c>reject</c>.</param>
    /// <param name="ct">Cancellation token.</param>
    internal async Task ReplyToPermissionAsync(string requestId, string reply, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(requestId))
            return;

        // Guard: drop duplicate concurrent calls for the same requestId (double-tap protection).
        if (!_inFlightPermissionReplies.Add(requestId))
            return;

        try
        {
            var replyLabel = reply switch
            {
                "always" => "Always",
                "once" => "Once",
                "reject" => "Deny",
                _ => reply,
            };

            var result = await _apiClient.ReplyToPermissionAsync(requestId, reply, ct);

            if (!result.IsSuccess)
            {
                SentryHelper.CaptureException(new InvalidOperationException(result.Error?.Message ?? "Permission reply failed"), new Dictionary<string, object>
                {
                    ["context"] = "ChatViewModel.ReplyToPermissionAsync",
                    ["requestId"] = requestId,
                    ["reply"] = reply,
                });

                return;
            }

            _dispatcher.Dispatch(() => ResolvePermissionRequest(requestId, reply, replyLabel));
        }
        catch (Exception ex)
        {
            SentryHelper.CaptureException(ex, new Dictionary<string, object>
            {
                ["context"] = "ChatViewModel.ReplyToPermissionAsync",
                ["requestId"] = requestId,
                ["reply"] = reply,
            });
        }
        finally
        {
            _inFlightPermissionReplies.Remove(requestId);
        }
    }

    /// <summary>Replies to a permission request with <c>always</c>.</summary>
    /// <param name="requestId">The permission request identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    [RelayCommand]
    private async Task ReplyToPermissionAlwaysAsync(string requestId, CancellationToken ct)
        => await ReplyToPermissionAsync(requestId, "always", ct);

    /// <summary>Replies to a permission request with <c>once</c>.</summary>
    /// <param name="requestId">The permission request identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    [RelayCommand]
    private async Task ReplyToPermissionOnceAsync(string requestId, CancellationToken ct)
        => await ReplyToPermissionAsync(requestId, "once", ct);

    /// <summary>Replies to a permission request with <c>reject</c>.</summary>
    /// <param name="requestId">The permission request identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    [RelayCommand]
    private async Task ReplyToPermissionDenyAsync(string requestId, CancellationToken ct)
        => await ReplyToPermissionAsync(requestId, "reject", ct);

    /// <summary>
    /// Submits the user's answer to a pending question card [REQ-014].
    /// Concurrent calls for the same <paramref name="args"/>[0] (questionId) are silently dropped.
    /// On success, resolves the card and sets <see cref="IsAiResponding"/> to <c>true</c>.
    /// On failure, captures the exception via Sentry and leaves the card in Pending state.
    /// </summary>
    /// <param name="args">
    /// A two-element array where <c>args[0]</c> is the question ID and <c>args[1]</c> is the answer text.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    [RelayCommand]
    private async Task AnswerQuestionAsync(string[] args, CancellationToken ct)
    {
        if (args is not { Length: 2 })
            return;

        var questionId = args[0];
        var answer = args[1];

        if (string.IsNullOrWhiteSpace(questionId) || string.IsNullOrWhiteSpace(answer))
            return;

        // Guard: drop duplicate concurrent calls for the same questionId
        if (!_inFlightQuestionAnswers.Add(questionId))
            return;

        try
        {
            var result = await _apiClient.ReplyToQuestionAsync(questionId, new[] { answer }, ct)
                .ConfigureAwait(false);

            if (!result.IsSuccess)
            {
                SentryHelper.CaptureException(
                    new InvalidOperationException(result.Error?.Message ?? "ReplyToQuestionAsync failed"),
                    new Dictionary<string, object>
                    {
                        ["context"] = "ChatViewModel.AnswerQuestionAsync",
                        ["questionId"] = questionId,
                        ["answer"] = answer,
                    });
                return;
            }

            _dispatcher.Dispatch(() =>
            {
                ResolveQuestionCard(questionId, answer);
                IsAiResponding = true; // [REQ-017] agent resumes after answer
            });
        }
        catch (Exception ex)
        {
            SentryHelper.CaptureException(ex, new Dictionary<string, object>
            {
                ["context"] = "ChatViewModel.AnswerQuestionAsync",
                ["questionId"] = questionId,
                ["answer"] = answer,
            });
        }
        finally
        {
            _inFlightQuestionAnswers.Remove(questionId);
        }
    }

    // ─── Grouping [REQ-016] ───────────────────────────────────────────────────

    /// <summary>
    /// Recalculates <see cref="ChatMessage.IsFirstInGroup"/> and
    /// <see cref="ChatMessage.IsLastInGroup"/> for all messages in the collection.
    /// A message is first-in-group if it is the first message or the previous message
    /// has a different <see cref="ChatMessage.IsFromUser"/> value.
    /// </summary>
    private void RecalculateGrouping()
    {
        for (var i = 0; i < Messages.Count; i++)
        {
            var msg = Messages[i];
            var prev = i > 0 ? Messages[i - 1] : null;
            var next = i < Messages.Count - 1 ? Messages[i + 1] : null;

            msg.IsFirstInGroup = prev is null || prev.IsFromUser != msg.IsFromUser;
            msg.IsLastInGroup = next is null || next.IsFromUser != msg.IsFromUser;
        }
    }

    /// <summary>Updates <see cref="IsEmpty"/> based on the current message count.</summary>
    private void UpdateIsEmpty() => IsEmpty = Messages.Count == 0;

    /// <summary>Resets the pending permission state.</summary>
    private void ResetPermissionState()
    {
        _pendingPermissionCount = 0;
        HasPendingPermissions = false;
    }

    /// <summary>Increments the pending permission counter and refreshes <see cref="HasPendingPermissions"/>.</summary>
    private void IncrementPendingPermissions()
    {
        _pendingPermissionCount++;
        HasPendingPermissions = _pendingPermissionCount > 0;
    }

    /// <summary>Decrements the pending permission counter and refreshes <see cref="HasPendingPermissions"/>.</summary>
    private void DecrementPendingPermissions()
    {
        if (_pendingPermissionCount > 0)
            _pendingPermissionCount--;

        HasPendingPermissions = _pendingPermissionCount > 0;
    }

    /// <summary>Marks a permission request as resolved when the API reply succeeds.</summary>
    /// <param name="requestId">The permission request identifier.</param>
    /// <param name="reply">The raw reply value.</param>
    /// <param name="replyLabel">The UI label for the reply.</param>
    private void ResolvePermissionRequest(string requestId, string reply, string replyLabel)
    {
        var message = FindPermissionMessageByRequestId(requestId);
        if (message is null || message.PermissionStatus == PermissionStatus.Resolved)
            return;

        message.PermissionStatus = PermissionStatus.Resolved;
        message.ResolvedReply = reply;
        message.ResolvedReplyLabel = replyLabel;
        DecrementPendingPermissions();
    }

    /// <summary>Resets the pending question state.</summary>
    private void ResetQuestionState()
    {
        _pendingQuestionCount = 0;
        HasPendingQuestions = false;
        _inFlightQuestionAnswers.Clear();
    }

    /// <summary>Increments the pending question counter and refreshes <see cref="HasPendingQuestions"/>.</summary>
    private void IncrementPendingQuestions()
    {
        _pendingQuestionCount++;
        HasPendingQuestions = _pendingQuestionCount > 0;
    }

    /// <summary>Decrements the pending question counter and refreshes <see cref="HasPendingQuestions"/>.</summary>
    private void DecrementPendingQuestions()
    {
        if (_pendingQuestionCount > 0)
            _pendingQuestionCount--;

        HasPendingQuestions = _pendingQuestionCount > 0;
    }

    /// <summary>Marks a question card as resolved when the API answer succeeds.</summary>
    /// <param name="questionId">The question request identifier.</param>
    /// <param name="answer">The answer submitted by the user.</param>
    private void ResolveQuestionCard(string questionId, string answer)
    {
        for (var i = 0; i < Messages.Count; i++)
        {
            var msg = Messages[i];
            if (msg.MessageKind == MessageKind.QuestionRequest &&
                msg.QuestionId == questionId &&
                msg.QuestionStatus == QuestionStatus.Pending)
            {
                msg.QuestionStatus = QuestionStatus.Resolved;
                msg.ResolvedAnswer = answer;
                DecrementPendingQuestions();
                return;
            }
        }
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Finds the index of the most recent optimistic user message placeholder.
    /// An optimistic message is identified by <see cref="ChatMessage.IsOptimistic"/> = true.
    /// Searches from the end of the collection (most recent first).
    /// </summary>
    /// <returns>The zero-based index of the matching message, or -1 if not found.</returns>
    private int FindOptimisticUserMessageIndex()
    {
        for (var i = Messages.Count - 1; i >= 0; i--)
        {
            if (Messages[i].IsFromUser && Messages[i].IsOptimistic)
                return i;
        }

        return -1;
    }

    /// <summary>Finds a message in the collection by its ID.</summary>
    /// <param name="messageId">The message ID to search for.</param>
    /// <returns>The matching <see cref="ChatMessage"/>, or <c>null</c> if not found.</returns>
    private ChatMessage? FindMessageById(string messageId)
    {
        for (var i = 0; i < Messages.Count; i++)
        {
            if (Messages[i].Id == messageId)
                return Messages[i];
        }

        return null;
    }

    /// <summary>
    /// Finds or creates a <see cref="ToolCallInfo"/> in the message's <see cref="ChatMessage.ToolCalls"/>
    /// collection and updates it from the part's <see cref="PartDto.State"/> JSON element.
    /// Must be called on the UI thread.
    /// </summary>
    private static void UpsertToolCall(ChatMessage message, PartDto part)
    {
        // Find existing or create new
        ToolCallInfo? toolCall = null;
        foreach (var tc in message.ToolCalls)
        {
            if (tc.PartId == part.Id)
            {
                toolCall = tc;
                break;
            }
        }

        if (toolCall is null)
        {
            toolCall = new ToolCallInfo(part.Id, part.ToolName ?? part.Id, part.CallId);
            message.ToolCalls.Add(toolCall);
        }

        // Parse state defensively
        if (part.State is not { } stateEl || stateEl.ValueKind != JsonValueKind.Object)
            return;

        if (!stateEl.TryGetProperty("status", out var statusEl) ||
            statusEl.ValueKind != JsonValueKind.String)
            return;

        var statusStr = statusEl.GetString();
        toolCall.Status = statusStr switch
        {
            "pending" => ToolCallStatus.Pending,
            "running" => ToolCallStatus.Running,
            "completed" => ToolCallStatus.Completed,
            "error" => ToolCallStatus.Error,
            _ => toolCall.Status,
        };

        if (stateEl.TryGetProperty("title", out var titleEl) &&
            titleEl.ValueKind == JsonValueKind.String)
        {
            toolCall.Title = titleEl.GetString();
        }

        if (stateEl.TryGetProperty("output", out var outputEl) &&
            outputEl.ValueKind == JsonValueKind.String)
        {
            toolCall.Output = outputEl.GetString();
        }

        if (stateEl.TryGetProperty("error", out var errorEl) &&
            errorEl.ValueKind == JsonValueKind.String)
        {
            toolCall.ErrorText = errorEl.GetString();
        }

        // Compute duration from time.start and time.end
        if (stateEl.TryGetProperty("time", out var timeEl) &&
            timeEl.ValueKind == JsonValueKind.Object &&
            timeEl.TryGetProperty("start", out var startEl) &&
            timeEl.TryGetProperty("end", out var endEl) &&
            startEl.ValueKind == JsonValueKind.Number &&
            endEl.ValueKind == JsonValueKind.Number)
        {
            toolCall.DurationMs = endEl.GetInt64() - startEl.GetInt64();
        }
    }

    /// <summary>
    /// Hides any tool call card whose <see cref="ToolCallInfo.CallId"/> matches the given call ID
    /// and whose <see cref="ToolCallInfo.ToolName"/> is <c>"question"</c>.
    /// Must be called on the dispatcher thread.
    /// </summary>
    /// <param name="callId">The tool call identifier to match against <see cref="ToolCallInfo.CallId"/>.</param>
    private void HideToolCallByCallId(string callId)
    {
        foreach (var msg in Messages)
        {
            if (msg.ToolCalls is null)
                continue;

            foreach (var tc in msg.ToolCalls)
            {
                if (string.Equals(tc.CallId, callId, StringComparison.Ordinal) &&
                    string.Equals(tc.ToolName, "question", StringComparison.OrdinalIgnoreCase))
                {
                    tc.IsHidden = true;
                }
            }
        }
    }

    /// <summary>Finds an inline permission message by request identifier.</summary>
    /// <param name="requestId">The permission request identifier.</param>
    /// <returns>The matching permission message, or <c>null</c>.</returns>
    private ChatMessage? FindPermissionMessageByRequestId(string requestId)
    {
        for (var i = 0; i < Messages.Count; i++)
        {
            if (Messages[i].MessageKind == MessageKind.PermissionRequest &&
                Messages[i].RequestId == requestId)
            {
                return Messages[i];
            }
        }

        return null;
    }

    /// <summary>
    /// Maps a <see cref="ChatServiceError"/> to a user-friendly error message string.
    /// </summary>
    /// <param name="error">The service error to map.</param>
    /// <returns>A localised, user-friendly error message.</returns>
    private static string MapChatServiceError(ChatServiceError error)
    {
        return error.Kind switch
        {
            ChatServiceErrorKind.NetworkError => "Network error. Please check your connection and try again.",
            ChatServiceErrorKind.ServerError => "The server encountered an error. Please try again later.",
            ChatServiceErrorKind.Timeout => "The request timed out. Please try again.",
            ChatServiceErrorKind.CircuitOpen => "The server is temporarily unavailable. Please wait a moment and try again.",
            ChatServiceErrorKind.Cancelled => "The operation was cancelled.",
            _ => $"An unexpected error occurred: {error.Message}",
        };
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
            ;

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
                ;

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
                ;

            if (deleted)
            {
                // Create a new session to replace the deleted one
                var newSession = await _sessionService.CreateSessionAsync(null, ct)
                    ;

                if (newSession is not null)
                {
                    CurrentSessionId = newSession.Id;
                    SessionName = newSession.Title;
                    // Notify FlyoutViewModel to highlight the new session
                    WeakReferenceMessenger.Default.Send(new CurrentSessionChangedMessage(newSession.Id));
                }
                else
                {
                    CurrentSessionId = null;
                    SessionName = "New chat";
                    // Notify FlyoutViewModel that no session is active
                    WeakReferenceMessenger.Default.Send(new CurrentSessionChangedMessage(null));
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

    // ─── Heartbeat Monitor [REQ-001, REQ-002, REQ-003, REQ-006] ─────────────

    /// <summary>
    /// Starts the heartbeat monitor and, if the connection is already <c>Lost</c>
    /// when the page re-appears (e.g. after returning from ServerManagementPage),
    /// immediately re-shows the reconnection modal without waiting for the next timer tick.
    /// Called from <c>ChatPage.OnAppearing</c>.
    /// </summary>
    /// <remarks>
    /// The monitor is intentionally NOT stopped when the user navigates to a child page
    /// (e.g. ServerManagementPage). It keeps running so the health state stays current.
    /// <see cref="StopHeartbeatMonitor"/> is only called when ChatPage fully disappears
    /// (e.g. navigation to Splash/Onboarding), not on child-page pushes.
    /// </remarks>
    /// <param name="ct">Cancellation token.</param>
    [RelayCommand]
    private async Task StartHeartbeatMonitorAsync(CancellationToken ct)
    {
        await _heartbeatMonitor.StartAsync(ct).ConfigureAwait(false);

        // If the connection was already Lost when the page re-appeared (e.g. the user
        // returned from ServerManagementPage without fixing the server), trigger the
        // modal immediately — do not wait for the next 5-second timer tick.
        if (_heartbeatMonitor.HealthState == ConnectionHealthState.Lost)
        {
            OnHealthStateChanged(ConnectionHealthState.Lost);
        }
    }

    /// <summary>
    /// Stops the heartbeat monitor. Called from <c>ChatPage.OnDisappearing</c>
    /// only when navigating away from the chat area entirely (not to child pages).
    /// </summary>
    [RelayCommand]
    private void StopHeartbeatMonitor()
    {
        _ = _heartbeatMonitor.StopAsync();
    }

    /// <summary>Called when a server.heartbeat SSE event is received. Resets the health state to Healthy.</summary>
    internal void OnHeartbeatReceived()
    {
        _heartbeatMonitor.RecordHeartbeat();
    }

    /// <summary>Handles health state changes from the heartbeat monitor.</summary>
    /// <remarks>
    /// The synchronous property update is dispatched to the UI thread immediately.
    /// When the state transitions to <see cref="ConnectionHealthState.Lost"/>, the modal
    /// is shown first (via <c>await</c>) before the reconnection loop starts, guaranteeing
    /// the modal is on the navigation stack before <see cref="ReconnectingModalViewModel.ReconnectionSucceeded"/>
    /// can fire and call <see cref="IAppPopupService.PopPopupAsync"/>.
    /// The reconnection loop is scoped to <c>_reconnectionCts</c> so it is cancelled when
    /// the ViewModel is disposed or a new Lost transition supersedes the current one.
    /// </remarks>
    private void OnHealthStateChanged(ConnectionHealthState newState)
    {
        // Capture previous state before updating [REQ-008].
        var previousState = _previousHealthState;
        _previousHealthState = newState;

        // Synchronous property update — must stay on the UI thread.
        _dispatcher.Dispatch(() =>
        {
            ConnectionHealthState = newState;
        });

        if (newState == ConnectionHealthState.Lost)
        {
            // Guard: if the modal is already visible (e.g. the user returned from
            // ServerManagementPage and OnAppearing re-triggered this path), do not
            // push a second modal on top of the existing one.
            if (_isReconnectingModalVisible)
                return;

            // Cancel any previous reconnection loop before starting a new one.
            _reconnectionCts?.Cancel();
            _reconnectionCts?.Dispose();
            _reconnectionCts = new CancellationTokenSource();
            var ct = _reconnectionCts.Token;

            _ = Task.Run(async () =>
            {
                try
                {
                    var vm = new ReconnectingModalViewModel(
                        _connectionManager,
                        _navigationService,
                        _popupService);

                    vm.ReconnectionSucceeded += () =>
                    {
                        _dispatcher.Dispatch(() =>
                        {
                            _isReconnectingModalVisible = false;
                            // Reset the monitor's internal state so the next timer tick
                            // does not immediately re-fire Lost.
                            _heartbeatMonitor.RecordHeartbeat();
                            _ = _popupService.PopPopupAsync();
                        });

                        // Restart the SSE subscription so the app resumes receiving
                        // real-time events (including heartbeats) from the server.
                        // The SSE loop exits when the server drops — it is not
                        // restarted automatically, so we must do it here on recovery.
                        if (CurrentSessionId is not null)
                            _ = StartSseSubscriptionAsync();
                    };

                    vm.ModalDismissedForNavigation += () =>
                    {
                        // The user tapped "Gestisci server" — the modal is being popped
                        // by the ViewModel before navigating. Mark it as no longer visible
                        // so OnAppearing can re-show it if the server is still down.
                        _isReconnectingModalVisible = false;
                    };

                    _isReconnectingModalVisible = true;

                    // Show modal FIRST so it is guaranteed on the navigation stack
                    // before StartReconnectionLoopAsync can raise ReconnectionSucceeded.
                    await _popupService.ShowReconnectingModalAsync(vm, ct).ConfigureAwait(false);
                    await vm.StartReconnectionLoopAsync(ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Expected when the ViewModel is disposed or a new Lost transition cancels this loop.
                    _isReconnectingModalVisible = false;
                }
                catch (Exception ex)
                {
                    _isReconnectingModalVisible = false;
                    SentryHelper.CaptureException(ex, new Dictionary<string, object>
                    {
                        ["context"] = "ChatViewModel.OnHealthStateChanged",
                    });
                }
            }, ct);
        }

        // [REQ-003, REQ-008] Replay pending permissions on Lost/Degraded → Healthy transition.
        // Fire-and-forget: do not block the HealthStateChanged callback thread [REQ-007].
        if (newState == ConnectionHealthState.Healthy &&
            previousState != ConnectionHealthState.Healthy)
        {
            _ = ReplayPendingPermissionsAsync(CancellationToken.None);
            _ = RecoverPendingQuestionsAsync(CancellationToken.None);
        }
    }

    /// <summary>
    /// Fetches all pending permissions for the active session and replies <c>"always"</c>
    /// to each one. Called fire-and-forget on <c>Lost/Degraded → Healthy</c> transitions
    /// when <see cref="AutoAccept"/> is enabled [REQ-003 through REQ-008].
    /// </summary>
    /// <remarks>
    /// <para>
    /// Calls <see cref="IOpencodeApiClient.GetPendingPermissionsAsync"/> which hits
    /// <c>GET /permission</c> (global endpoint) and filters by <see cref="CurrentSessionId"/>.
    /// </para>
    /// <para>
    /// Replies are sent sequentially to avoid race conditions on the server's approved ruleset.
    /// Each reply goes through <see cref="ReplyToPermissionAsync"/> which applies the
    /// <c>_inFlightPermissionReplies</c> duplicate guard automatically.
    /// </para>
    /// </remarks>
    /// <param name="ct">Cancellation token.</param>
    private async Task ReplayPendingPermissionsAsync(CancellationToken ct)
    {
        try
        {
            // [REQ-004a] No-op if AutoAccept is disabled.
            if (!AutoAccept)
                return;

            // [REQ-004b] No-op if no active session.
            var sessionId = CurrentSessionId;
            if (string.IsNullOrEmpty(sessionId))
                return;

            // [REQ-004c] Fetch all pending permissions for the active session.
            var result = await _apiClient.GetPendingPermissionsAsync(sessionId, ct)
                .ConfigureAwait(false);

            if (!result.IsSuccess)
            {
                // [REQ-005] Capture fetch failure silently.
                SentryHelper.CaptureException(
                    new InvalidOperationException(result.Error?.Message ?? "GetPendingPermissionsAsync failed"),
                    new Dictionary<string, object>
                    {
                        ["context"] = "ChatViewModel.ReplayPendingPermissionsAsync",
                        ["sessionId"] = sessionId,
                    });
                return;
            }

            var permissions = result.Value;
            if (permissions is null || permissions.Count == 0)
                return;

            // [REQ-004d, REQ-004e] Reply sequentially to avoid server-side race conditions.
            foreach (var permission in permissions)
            {
                try
                {
                    // ReplyToPermissionAsync applies _inFlightPermissionReplies guard [REQ-004d, AC-008].
                    await ReplyToPermissionAsync(permission.Id, "always", ct)
                        .ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    // [REQ-006] Capture per-reply failure and continue to next permission.
                    SentryHelper.CaptureException(ex, new Dictionary<string, object>
                    {
                        ["context"] = "ChatViewModel.ReplayPendingPermissionsAsync.Reply",
                        ["requestId"] = permission.Id,
                        ["sessionId"] = sessionId,
                    });
                }
            }
        }
        catch (Exception ex)
        {
            // Outer catch prevents unobserved task exceptions from the fire-and-forget call.
            SentryHelper.CaptureException(ex, new Dictionary<string, object>
            {
                ["context"] = "ChatViewModel.ReplayPendingPermissionsAsync.Outer",
            });
        }
    }

    // ─── Active Project Change Handler [REQ-009] ───────────────────────────────

    /// <summary>
    /// Handles the <see cref="ActiveProjectChangedMessage"/> by navigating to the most
    /// recent session of the new active project, or to a new chat if no sessions exist (REQ-009).
    /// Navigation is dispatched to the UI thread via <see cref="IDispatcherService"/>.
    /// </summary>
    /// <param name="message">The active project changed message containing the new project.</param>
    private async Task HandleActiveProjectChangedAsync(ActiveProjectChangedMessage message)
    {
        try
        {
            var projectId = message.Project.Id;

            // Get the most recent session for the new project
            var lastSession = await _sessionService.GetLastSessionForProjectAsync(projectId);

            if (lastSession is not null)
            {
                // Navigate to the most recent session
                _dispatcher.Dispatch(async () =>
                {
                    await _navigationService.GoToAsync("//chat", new Dictionary<string, object>
                    {
                        ["sessionId"] = lastSession.Id,
                    });
                });
            }
            else
            {
                // Navigate to new session for this project (no sessions exist)
                _dispatcher.Dispatch(async () =>
                {
                    await _navigationService.GoToAsync("//chat");
                });
            }
        }
        catch (Exception ex)
        {
            SentryHelper.CaptureException(ex, new Dictionary<string, object>
            {
                ["context"] = "ChatViewModel.HandleActiveProjectChangedAsync",
            });
        }
    }

    // ─── IDisposable [REQ-018] ────────────────────────────────────────────────

    /// <summary>
    /// Releases resources held by this ViewModel: cancels the SSE subscription,
    /// cancels any active reconnection loop, unsubscribes from heartbeat monitor events,
    /// and clears collections.
    /// </summary>
    public void Dispose()
    {
        WeakReferenceMessenger.Default.UnregisterAll(this);
        _heartbeatMonitor.HealthStateChanged -= OnHealthStateChanged;
        _ = _heartbeatMonitor.StopAsync();
        _sseCts?.Cancel();
        _sseCts?.Dispose();
        _sseCts = null;
        _reconnectionCts?.Cancel();
        _reconnectionCts?.Dispose();
        _reconnectionCts = null;
        Messages.Clear();
        _inFlightPermissionReplies.Clear();
        _inFlightQuestionAnswers.Clear();
        SuggestionChips.Clear();
    }
}
