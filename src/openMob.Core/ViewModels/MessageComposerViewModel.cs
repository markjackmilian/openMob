using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using openMob.Core.Helpers;
using openMob.Core.Messages;
using openMob.Core.Models;
using openMob.Core.Services;

namespace openMob.Core.ViewModels;

/// <summary>
/// ViewModel for the message composer popup (FAB trigger).
/// Manages message text, draft persistence, agent/thinking-level overrides,
/// file attachment, command insertion, and message dispatch.
/// </summary>
public partial class MessageComposerViewModel : ObservableObject, IDisposable
{
    private readonly IProjectPreferenceService _preferenceService;
    private readonly IAppPopupService _popupService;
    private readonly IDraftService _draftService;
    private readonly IDispatcherService _dispatcher;

    /// <summary>Initialises the message composer ViewModel with required dependencies.</summary>
    /// <param name="preferenceService">Service for loading project preferences.</param>
    /// <param name="popupService">Service for popup/dialog operations.</param>
    /// <param name="draftService">Service for in-memory draft persistence.</param>
    /// <param name="dispatcher">Service for UI thread dispatching.</param>
    public MessageComposerViewModel(
        IProjectPreferenceService preferenceService,
        IAppPopupService popupService,
        IDraftService draftService,
        IDispatcherService dispatcher)
    {
        ArgumentNullException.ThrowIfNull(preferenceService);
        ArgumentNullException.ThrowIfNull(popupService);
        ArgumentNullException.ThrowIfNull(draftService);
        ArgumentNullException.ThrowIfNull(dispatcher);

        _preferenceService = preferenceService;
        _popupService = popupService;
        _draftService = draftService;
        _dispatcher = dispatcher;

        // Subscribe to streaming state changes from ChatViewModel [REQ-016]
        WeakReferenceMessenger.Default.Register<StreamingStateChangedMessage>(this, (r, m) =>
        {
            var vm = (MessageComposerViewModel)r;
            vm._dispatcher.Dispatch(() =>
            {
                vm.IsStreaming = m.IsStreaming;
            });
        });
    }

    // ─── Initialisation context (set before popup opens) ─────────────────────

    /// <summary>The project ID for the current session.</summary>
    public string ProjectId { get; private set; } = string.Empty;

    /// <summary>The session ID for the current session.</summary>
    public string SessionId { get; private set; } = string.Empty;

    // ─── Bindable properties ──────────────────────────────────────────────────

    /// <summary>The message text bound to the editor.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    private string _messageText = string.Empty;

    /// <summary>The agent name override for this session, or <c>null</c> for default [REQ-010].</summary>
    [ObservableProperty]
    private string? _sessionAgentName;

    /// <summary>Display name for the session agent override.</summary>
    public string SessionAgentDisplayName => SessionAgentName ?? "Default";

    /// <summary>The model ID override for this session (e.g., "anthropic/claude-sonnet-4-5"), or <c>null</c> for default.</summary>
    [ObservableProperty]
    private string? _sessionModelId;

    /// <summary>Display name for the session model override.</summary>
    public string SessionModelDisplayName =>
        SessionModelId is not null ? ModelIdHelper.ExtractModelName(SessionModelId) : "No model";

    /// <summary>The thinking-level override for this session [REQ-011].</summary>
    [ObservableProperty]
    private ThinkingLevel _sessionThinkingLevel = ThinkingLevel.Medium;

    /// <summary>Whether the thinking-level inline control is expanded [REQ-011].</summary>
    [ObservableProperty]
    private bool _isThinkLevelExpanded;

    /// <summary>Whether auto-accept is enabled for this session [REQ-012].</summary>
    [ObservableProperty]
    private bool _sessionAutoAccept;

    /// <summary>Whether the AI is currently streaming a response [REQ-016].</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    private bool _isStreaming;

    /// <summary>The send button text — changes based on streaming state.</summary>
    public string SendButtonText => IsStreaming ? "Attendi risposta…" : "Invia";

    // ─── Source-generated partial methods ─────────────────────────────────────

    /// <summary>Updates <see cref="SessionAgentDisplayName"/> when agent name changes.</summary>
    partial void OnSessionAgentNameChanged(string? value)
    {
        OnPropertyChanged(nameof(SessionAgentDisplayName));
    }

    /// <summary>Updates <see cref="SessionModelDisplayName"/> when model ID changes.</summary>
    partial void OnSessionModelIdChanged(string? value)
    {
        OnPropertyChanged(nameof(SessionModelDisplayName));
    }

    /// <summary>Updates <see cref="SendButtonText"/> when streaming state changes.</summary>
    partial void OnIsStreamingChanged(bool value)
    {
        OnPropertyChanged(nameof(SendButtonText));
    }

    // ─── Initialisation ───────────────────────────────────────────────────────

    /// <summary>
    /// Initialises the composer with project/session context and restores any saved draft [REQ-008, REQ-009].
    /// Called by <see cref="IAppPopupService.ShowMessageComposerAsync"/> before the popup is presented.
    /// </summary>
    /// <param name="projectId">The project identifier.</param>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="isStreaming">Whether the AI is currently streaming.</param>
    /// <param name="currentModelId">The currently active model ID from ChatViewModel, or <c>null</c>.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task InitializeAsync(string projectId, string sessionId, bool isStreaming, string? currentModelId = null, CancellationToken ct = default)
    {
        ProjectId = projectId;
        SessionId = sessionId;
        IsStreaming = isStreaming;

        // Load preferences
        var pref = await _preferenceService.GetOrDefaultAsync(projectId, ct);
        SessionAgentName = pref.AgentName;
        SessionModelId = currentModelId ?? pref.DefaultModelId;
        SessionThinkingLevel = pref.ThinkingLevel;
        SessionAutoAccept = pref.AutoAccept;

        // Restore draft
        MessageText = _draftService.GetDraft(sessionId) ?? string.Empty;
    }

    // ─── Commands ─────────────────────────────────────────────────────────────

    /// <summary>Opens the agent picker popup in primary-agent selection mode [REQ-010].</summary>
    [RelayCommand]
    private async Task SelectAgentAsync(CancellationToken ct)
    {
        await _popupService.ShowAgentPickerAsync(agentName =>
        {
            SessionAgentName = agentName;
        }, ct);
    }

    /// <summary>Opens the model picker popup to select an AI model.</summary>
    [RelayCommand]
    private async Task SelectModelAsync(CancellationToken ct)
    {
        await _popupService.ShowModelPickerAsync(modelId =>
        {
            SessionModelId = modelId;
        }, ct);
    }

    /// <summary>Sets the thinking level from the inline segmented control [REQ-011].</summary>
    [RelayCommand]
    private void SetThinkingLevel(ThinkingLevel level)
    {
        SessionThinkingLevel = level;
        IsThinkLevelExpanded = false;
    }

    /// <summary>Toggles the thinking-level inline control expansion [REQ-011].</summary>
    [RelayCommand]
    private void ToggleThinkLevelExpanded()
    {
        IsThinkLevelExpanded = !IsThinkLevelExpanded;
    }

    /// <summary>Toggles auto-accept for this session [REQ-012].</summary>
    [RelayCommand]
    private void ToggleAutoAccept()
    {
        SessionAutoAccept = !SessionAutoAccept;
    }

    /// <summary>Opens the agent picker in subagent mode and inserts @agentName token [REQ-013].</summary>
    [RelayCommand]
    private async Task InsertSubagentAsync(CancellationToken ct)
    {
        await _popupService.ShowSubagentPickerAsync(name => InsertToken($"@{name}"), ct);
    }

    /// <summary>Opens the command palette in callback mode and inserts /commandName token [REQ-014].</summary>
    [RelayCommand]
    private async Task InsertCommandAsync(CancellationToken ct)
    {
        await _popupService.ShowCommandPaletteAsync(cmd => InsertToken($"/{cmd}"), ct);
    }

    /// <summary>Opens the file picker and inserts @relativePath token [REQ-015].</summary>
    [RelayCommand]
    private async Task InsertFileAsync(CancellationToken ct)
    {
        await _popupService.ShowFilePickerAsync(path => InsertToken($"@{path}"), ct);
    }

    /// <summary>
    /// Sends the composed message [REQ-017].
    /// Dispatches a <see cref="MessageComposedMessage"/> via <see cref="WeakReferenceMessenger"/>,
    /// clears the draft, and pops the popup.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanSend))]
    private async Task SendAsync(CancellationToken ct)
    {
        if (!CanSend())
            return;

        var text = MessageText;
        MessageText = string.Empty;

        WeakReferenceMessenger.Default.Send(new MessageComposedMessage(
            ProjectId,
            SessionId,
            text,
            SessionAgentName,
            SessionModelId,
            SessionThinkingLevel,
            SessionAutoAccept));

        _draftService.ClearDraft(SessionId);

        await _popupService.PopPopupAsync(ct);
    }

    /// <summary>
    /// Closes the composer popup without sending [REQ-018].
    /// Saves the current text as a draft before closing.
    /// </summary>
    [RelayCommand]
    private async Task CloseAsync(CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(MessageText))
        {
            _draftService.SaveDraft(SessionId, MessageText);
        }

        await _popupService.PopPopupAsync(ct);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>Determines whether <see cref="SendCommand"/> can execute.</summary>
    private bool CanSend() => !string.IsNullOrWhiteSpace(MessageText) && !IsStreaming;

    /// <summary>Appends a token to <see cref="MessageText"/> with a leading space if non-empty.</summary>
    private void InsertToken(string token)
    {
        var text = MessageText;
        MessageText = string.IsNullOrEmpty(text)
            ? token
            : $"{text} {token}";
    }

    // ─── Dispose ──────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public void Dispose()
    {
        WeakReferenceMessenger.Default.UnregisterAll(this);

        // Save draft on dispose (e.g. popup dismissed without explicit close)
        if (!string.IsNullOrWhiteSpace(MessageText) && !string.IsNullOrEmpty(SessionId))
        {
            _draftService.SaveDraft(SessionId, MessageText);
        }
    }
}
