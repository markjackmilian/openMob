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
    private readonly IProviderService _providerService;
    private readonly IProjectPreferenceService _preferenceService;
    private readonly IChatService _chatService;
    private readonly IOpencodeApiClient _apiClient;
    private readonly IDispatcherService _dispatcher;
    private readonly IActiveProjectService _activeProjectService;

    /// <summary>Cancellation token source for the active SSE subscription.</summary>
    private CancellationTokenSource? _sseCts;

    /// <summary>
    /// The absolute path of the current project's working directory, used to filter
    /// incoming SSE events by project context (REQ-004). Set once during
    /// <see cref="LoadContextAsync"/> from <see cref="IActiveProjectService.GetCachedWorktree"/>.
    /// </summary>
    private string? _currentProjectDirectory;

    /// <summary>Initialises the ChatViewModel with required dependencies.</summary>
    /// <param name="projectService">Service for project operations.</param>
    /// <param name="sessionService">Service for session operations.</param>
    /// <param name="navigationService">Service for Shell navigation.</param>
    /// <param name="popupService">Service for popup/dialog operations.</param>
    /// <param name="connectionManager">Manages the opencode server connection state.</param>
    /// <param name="providerService">Service for AI provider operations.</param>
    /// <param name="preferenceService">Service for per-project preference persistence.</param>
    /// <param name="chatService">Service for chat operations (messages, prompts, SSE).</param>
    /// <param name="apiClient">Low-level opencode API client (used for session abort).</param>
    /// <param name="dispatcher">UI thread dispatcher for thread-safe collection updates.</param>
    /// <param name="activeProjectService">Service for managing the client-side active project state.</param>
    public ChatViewModel(
        IProjectService projectService,
        ISessionService sessionService,
        INavigationService navigationService,
        IAppPopupService popupService,
        IOpencodeConnectionManager connectionManager,
        IProviderService providerService,
        IProjectPreferenceService preferenceService,
        IChatService chatService,
        IOpencodeApiClient apiClient,
        IDispatcherService dispatcher,
        IActiveProjectService activeProjectService)
    {
        ArgumentNullException.ThrowIfNull(projectService);
        ArgumentNullException.ThrowIfNull(sessionService);
        ArgumentNullException.ThrowIfNull(navigationService);
        ArgumentNullException.ThrowIfNull(popupService);
        ArgumentNullException.ThrowIfNull(connectionManager);
        ArgumentNullException.ThrowIfNull(providerService);
        ArgumentNullException.ThrowIfNull(preferenceService);
        ArgumentNullException.ThrowIfNull(chatService);
        ArgumentNullException.ThrowIfNull(apiClient);
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(activeProjectService);

        _projectService = projectService;
        _sessionService = sessionService;
        _navigationService = navigationService;
        _popupService = popupService;
        _connectionManager = connectionManager;
        _providerService = providerService;
        _preferenceService = preferenceService;
        _chatService = chatService;
        _apiClient = apiClient;
        _dispatcher = dispatcher;
        _activeProjectService = activeProjectService;

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

    // ─── Chat Page Redesign Properties [REQ-019, REQ-022, REQ-028, REQ-032] ──

    /// <summary>Gets or sets the current thinking/reasoning level, synced from Context Sheet.</summary>
    [ObservableProperty]
    private ThinkingLevel _thinkingLevel = ThinkingLevel.Medium;

    /// <summary>Gets or sets whether auto-accept is enabled for agent suggestions.</summary>
    [ObservableProperty]
    private bool _autoAccept;

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

        LoadMessagesCommand.Execute(null);
    }

    // ─── Existing Commands ────────────────────────────────────────────────────

    /// <summary>
    /// Loads the current project and session info, subscribes to connection status changes,
    /// and evaluates the status banner state.
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
            // Subscribe to connection status changes for status banner updates
            _connectionManager.StatusChanged -= OnConnectionStatusChanged;
            _connectionManager.StatusChanged += OnConnectionStatusChanged;

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

            // Evaluate provider state
            HasNoProvider = !await _providerService.HasAnyProviderConfiguredAsync(ct);

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
                        Messages.Add(ChatMessage.FromDto(dto));
                    }

                    RecalculateGrouping();
                    UpdateIsEmpty();
                });

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

            if (existing is not null &&
                string.Equals(e.Part.Type, "text", StringComparison.OrdinalIgnoreCase))
            {
                // The opencode server returns text directly in the "text" field of the part DTO.
                if (!string.IsNullOrEmpty(e.Part.Text))
                {
                    existing.TextContent = e.Part.Text;
                }

                existing.IsStreaming = true;
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

        if (!string.Equals(e.Field, "text", StringComparison.OrdinalIgnoreCase))
            return;

        _dispatcher.Dispatch(() =>
        {
            var existing = FindMessageById(e.MessageId);

            if (existing is not null)
            {
                // Append the delta to the existing text content
                existing.TextContent += e.Delta;
                existing.IsStreaming = true;
            }
            else
            {
                // Message not yet in collection — create a placeholder assistant message
                var placeholder = new ChatMessage(
                    id: e.MessageId,
                    sessionId: e.SessionId,
                    isFromUser: false,
                    textContent: e.Delta,
                    timestamp: DateTimeOffset.UtcNow,
                    deliveryStatus: MessageDeliveryStatus.Sent,
                    isStreaming: true);
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

    // ─── Status banner logic ──────────────────────────────────────────────────

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
                ActionLabel: "Gestisci server",
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

    // ─── Server Management Navigation [REQ-010] ──────────────────────────────

    /// <summary>
    /// Navigates to the Server Management page to allow the user to verify or update
    /// the server configuration. Uses <c>"///server-management"</c> (push onto the Shell
    /// navigation stack) to preserve back navigation to ChatPage (REQ-011).
    /// </summary>
    /// <remarks>
    /// <c>"///server-management"</c> is required because <c>server-management</c> is declared
    /// as a <c>ShellContent</c> in <c>AppShell.xaml</c>. MAUI does not allow plain relative
    /// routing to Shell elements — the triple-slash prefix performs a push navigation that
    /// keeps the back stack intact.
    /// </remarks>
    /// <param name="ct">Cancellation token.</param>
    [RelayCommand]
    private async Task NavigateToServerManagementAsync(CancellationToken ct)
    {
#if DEBUG
        var sw = Stopwatch.StartNew();
        DebugLogger.LogCommand(nameof(NavigateToServerManagementAsync), "start");
        try
        {
#endif
        await _navigationService.GoToAsync("///server-management", ct);
#if DEBUG
        sw.Stop();
        DebugLogger.LogCommand(nameof(NavigateToServerManagementAsync), "complete", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            DebugLogger.LogCommand(nameof(NavigateToServerManagementAsync), "failed", error: $"{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            throw;
        }
#endif
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
    /// unsubscribes from connection status events, and clears collections.
    /// </summary>
    public void Dispose()
    {
        WeakReferenceMessenger.Default.UnregisterAll(this);
        _connectionManager.StatusChanged -= OnConnectionStatusChanged;
        _sseCts?.Cancel();
        _sseCts?.Dispose();
        _sseCts = null;
        Messages.Clear();
        SuggestionChips.Clear();
    }
}
