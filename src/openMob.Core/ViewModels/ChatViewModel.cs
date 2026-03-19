using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using openMob.Core.Helpers;
using openMob.Core.Infrastructure.Http;
using openMob.Core.Infrastructure.Http.Dtos.Opencode;
using openMob.Core.Infrastructure.Monitoring;
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

    /// <summary>Cancellation token source for the active SSE subscription.</summary>
    private CancellationTokenSource? _sseCts;

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
        IDispatcherService dispatcher)
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

                // Load default model preference for this project
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
    /// Signals intent to open the ProjectSwitcherSheet popup.
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
    /// current session context.
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
    }

    /// <summary>
    /// Shows the More Menu option sheet and routes the selected action.
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
                // Open the model picker and update in-memory selection.
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
        if (CurrentSessionId is null)
            return;

        IsBusy = true;
        ErrorMessage = null;

        try
        {
            var result = await _chatService.GetMessagesAsync(CurrentSessionId, ct: ct).ConfigureAwait(false);

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

                // Start SSE subscription only after messages loaded successfully [REQ-011]
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
                ct).ConfigureAwait(false);

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
        if (CurrentSessionId is null)
            return;

        try
        {
            await _apiClient.AbortSessionAsync(CurrentSessionId, ct).ConfigureAwait(false);
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
    }

    /// <summary>
    /// Handles a suggestion chip tap [REQ-009]. Sets <see cref="InputText"/> to the
    /// chip's prompt text and invokes <see cref="SendMessageCommand"/>.
    /// </summary>
    /// <param name="chip">The selected suggestion chip.</param>
    [RelayCommand]
    private async Task SelectSuggestionChipAsync(SuggestionChip chip)
    {
        ArgumentNullException.ThrowIfNull(chip);

        InputText = chip.PromptText;
        await SendMessageCommand.ExecuteAsync(null);
    }

    /// <summary>
    /// Dismisses the current error message [REQ-010].
    /// </summary>
    [RelayCommand]
    private void DismissError()
    {
        ErrorMessage = null;
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
        await HandleRenameSessionAsync(ct);
    }

    /// <summary>
    /// Opens the Context Sheet bottom sheet (REQ-025).
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    [RelayCommand]
    private async Task OpenContextSheetAsync(CancellationToken ct)
    {
        await _popupService.ShowContextSheetAsync(ct);
    }

    /// <summary>
    /// Opens the Command Palette bottom sheet (REQ-029).
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    [RelayCommand]
    private async Task OpenCommandPaletteAsync(CancellationToken ct)
    {
        await _popupService.ShowCommandPaletteAsync(ct);
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
            await foreach (var chatEvent in _chatService.SubscribeToEventsAsync(ct).ConfigureAwait(false))
            {
                // Diagnostic: log every SSE event received
                var rawInfo = chatEvent is openMob.Core.Models.UnknownEvent unk
                    ? $" rawType='{unk.RawType}' rawData={unk.RawData?.ToString()?.Substring(0, Math.Min(200, unk.RawData?.ToString()?.Length ?? 0))}"
                    : string.Empty;
                System.Diagnostics.Debug.WriteLine($"[SSE] event received: {chatEvent.GetType().Name} (type={chatEvent.Type}){rawInfo}");

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
        // Diagnostic: log session ID mismatch to help identify filtering issues
        System.Diagnostics.Debug.WriteLine(
            $"[SSE] message.updated — event.sessionId='{e.Message.Info.SessionId}' current='{CurrentSessionId}' match={e.Message.Info.SessionId == CurrentSessionId}");

        if (e.Message.Info.SessionId != CurrentSessionId)
            return;

        _dispatcher.Dispatch(() =>
        {
            var existing = FindMessageById(e.Message.Info.Id);

            if (existing is not null)
            {
                existing.TextContent = ChatMessage.ExtractTextContent(e.Message.Parts ?? []);
                existing.IsStreaming = !existing.IsFromUser && !ChatMessage.HasCompletedTimestamp(e.Message.Info.Time);
                existing.DeliveryStatus = MessageDeliveryStatus.Sent;
            }
            else
            {
                var newMessage = ChatMessage.FromDto(e.Message);
                Messages.Add(newMessage);
                existing = newMessage;
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
    /// Updates the session title when the server renames the session.
    /// </summary>
    /// <param name="e">The session updated event.</param>
    private void HandleSessionUpdated(SessionUpdatedEvent e)
    {
        if (e.Session.Id != CurrentSessionId)
            return;

        _dispatcher.Dispatch(() =>
        {
            SessionName = e.Session.Title;
        });
    }

    /// <summary>
    /// Handles a <see cref="SessionErrorEvent"/> from the SSE stream [REQ-015].
    /// Sets the error state and marks the last user message as failed.
    /// </summary>
    /// <param name="e">The session error event.</param>
    private void HandleSessionError(SessionErrorEvent e)
    {
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

    // ─── IDisposable [REQ-018] ────────────────────────────────────────────────

    /// <summary>
    /// Releases resources held by this ViewModel: cancels the SSE subscription,
    /// unsubscribes from connection status events, and clears collections.
    /// </summary>
    public void Dispose()
    {
        _connectionManager.StatusChanged -= OnConnectionStatusChanged;
        _sseCts?.Cancel();
        _sseCts?.Dispose();
        _sseCts = null;
        Messages.Clear();
        SuggestionChips.Clear();
    }
}
