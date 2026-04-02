using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using openMob.Core.Data.Entities;
using openMob.Core.Helpers;
using openMob.Core.Infrastructure.Http.Dtos.Opencode;
using openMob.Core.Infrastructure.Logging;
using openMob.Core.Infrastructure.Monitoring;
using openMob.Core.Messages;
using openMob.Core.Models;
using openMob.Core.Services;

namespace openMob.Core.ViewModels;

/// <summary>
/// ViewModel for the Context Sheet bottom sheet.
/// Loads per-project preferences via <see cref="IProjectPreferenceService.GetOrDefaultAsync"/>,
/// exposes them as observable properties, and auto-saves every change.
/// On successful save, publishes a <see cref="ProjectPreferenceChangedMessage"/>
/// via <see cref="WeakReferenceMessenger.Default"/> so <see cref="ChatViewModel"/>
/// can update its state without a page reload.
/// Also exposes <see cref="DeleteSessionCommand"/> to delete the current session
/// after user confirmation, dismiss the sheet, and navigate to a new chat.
/// Also exposes <see cref="ShowUnhandledSseEvents"/> to control whether unhandled SSE event
/// debug cards are shown in the chat.
/// </summary>
/// <remarks>
/// Registered as Transient — a new instance is created each time the sheet is opened.
/// <see cref="InitializeAsync"/> must be called by the MAUI layer (via
/// <see cref="openMob.Core.Services.IAppPopupService.ShowContextSheetAsync"/>) immediately
/// after the sheet is resolved from DI and before it is pushed modally.
/// </remarks>
public sealed partial class ContextSheetViewModel : ObservableObject
{
    private readonly IProjectService _projectService;
    private readonly IProjectPreferenceService _preferenceService;
    private readonly IAppPopupService _popupService;
    private readonly IAgentService _agentService;
    private readonly INavigationService _navigationService;
    private readonly ISessionService _sessionService;

    /// <summary>Tracks the current project ID for save operations.</summary>
    private string? _currentProjectId;

    /// <summary>Tracks the current session ID for delete operations.</summary>
    private string? _currentSessionId;

    /// <summary>
    /// Prevents auto-save partial methods from firing during <see cref="InitializeAsync"/>.
    /// Set to true at the start of initialization, false when complete.
    /// </summary>
    private bool _isInitializing;

    /// <summary>Cached list of available subagents, loaded during <see cref="InitializeAsync"/> for CanExecute evaluation.</summary>
    private IReadOnlyList<AgentDto> _subagentAgents = [];

    /// <summary>
    /// Prevents <see cref="OnAutoAcceptChanged"/> from firing a save during
    /// <see cref="ToggleAutoAcceptCommand"/> execution (including rollback).
    /// </summary>
    private bool _isTogglingAutoAccept;

    /// <summary>Initialises the ContextSheetViewModel with required dependencies.</summary>
    /// <param name="projectService">Service for project operations.</param>
    /// <param name="preferenceService">Service for per-project preference persistence.</param>
    /// <param name="popupService">Service for opening picker popups from the Core layer.</param>
    /// <param name="agentService">Service for agent operations, used to load the subagent list for CanExecute evaluation.</param>
    /// <param name="navigationService">Service for Shell navigation, used for post-delete navigation.</param>
    /// <param name="sessionService">Service for session operations, used by <see cref="DeleteSessionCommand"/>.</param>
    public ContextSheetViewModel(
        IProjectService projectService,
        IProjectPreferenceService preferenceService,
        IAppPopupService popupService,
        IAgentService agentService,
        INavigationService navigationService,
        ISessionService sessionService)
    {
        ArgumentNullException.ThrowIfNull(projectService);
        ArgumentNullException.ThrowIfNull(preferenceService);
        ArgumentNullException.ThrowIfNull(popupService);
        ArgumentNullException.ThrowIfNull(agentService);
        ArgumentNullException.ThrowIfNull(navigationService);
        ArgumentNullException.ThrowIfNull(sessionService);

        _projectService = projectService;
        _preferenceService = preferenceService;
        _popupService = popupService;
        _agentService = agentService;
        _navigationService = navigationService;
        _sessionService = sessionService;
    }

    // ─── Observable Properties ────────────────────────────────────────────────

    /// <summary>Gets or sets the current project display name.</summary>
    [ObservableProperty]
    private string _projectName = "No project";

    /// <summary>
    /// Gets or sets the current agent name. Null means the default agent.
    /// Changing this property triggers auto-save via <see cref="OnSelectedAgentNameChanged"/>.
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

    /// <summary>
    /// Gets or sets the current model identifier in "providerId/modelId" format.
    /// Null when no model is selected.
    /// Changing this property triggers auto-save via <see cref="OnSelectedModelIdChanged"/>.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedModelDisplayName))]
    private string? _selectedModelId;

    /// <summary>
    /// Gets the display name for the selected model.
    /// Returns "No model" when <see cref="SelectedModelId"/> is null.
    /// </summary>
    public string SelectedModelDisplayName =>
        SelectedModelId is not null ? ModelIdHelper.ExtractModelName(SelectedModelId) : "No model";

    /// <summary>
    /// Gets or sets the current thinking/reasoning level.
    /// Changing this property triggers auto-save via <see cref="OnThinkingLevelChanged"/>.
    /// </summary>
    [ObservableProperty]
    private ThinkingLevel _thinkingLevel = ThinkingLevel.Medium;

    /// <summary>
    /// Gets or sets whether auto-accept is enabled for agent tool suggestions.
    /// Changing this property triggers auto-save via <see cref="OnAutoAcceptChanged"/>.
    /// </summary>
    [ObservableProperty]
    private bool _autoAccept;

    /// <summary>
    /// Gets or sets whether unhandled SSE event debug cards are shown in the chat.
    /// Changing this property triggers auto-save via <see cref="OnShowUnhandledSseEventsChanged"/>.
    /// </summary>
    [ObservableProperty]
    private bool _showUnhandledSseEvents;

    /// <summary>Gets or sets whether the sheet is currently loading preferences.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(InvokeSubagentCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteSessionCommand))]
    private bool _isBusy;

    /// <summary>
    /// Gets or sets the last save error message.
    /// Set when a preference save fails. The UI value is not rolled back on failure.
    /// Null when no error has occurred.
    /// </summary>
    [ObservableProperty]
    private string? _errorMessage;

    // ─── Commands ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Opens the agent picker popup in primary-agent selection mode.
    /// On selection, <see cref="SelectedAgentName"/> is updated, which triggers
    /// auto-save via <see cref="OnSelectedAgentNameChanged"/> and publishes
    /// a <see cref="ProjectPreferenceChangedMessage"/>.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    [RelayCommand]
    private async Task SelectAgentAsync(CancellationToken ct)
    {
#if DEBUG
        var sw = Stopwatch.StartNew();
        DebugLogger.LogCommand(nameof(SelectAgentAsync), "start");
        try
        {
#endif
        await _popupService.ShowAgentPickerAsync(agentName =>
        {
            SelectedAgentName = agentName;
        }, ct);
#if DEBUG
        sw.Stop();
        DebugLogger.LogCommand(nameof(SelectAgentAsync), "complete", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            DebugLogger.LogCommand(nameof(SelectAgentAsync), "failed", error: $"{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            throw;
        }
#endif
    }

    /// <summary>
    /// Opens the model picker popup and updates <see cref="SelectedModelId"/> on selection.
    /// The property change triggers auto-save via <see cref="OnSelectedModelIdChanged"/>.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    [RelayCommand]
    private async Task SelectModelAsync(CancellationToken ct)
    {
#if DEBUG
        var sw = Stopwatch.StartNew();
        DebugLogger.LogCommand(nameof(SelectModelAsync), "start");
        try
        {
#endif
        await _popupService.ShowModelPickerAsync(modelId =>
        {
            SelectedModelId = modelId;
        }, ct);
#if DEBUG
        sw.Stop();
        DebugLogger.LogCommand(nameof(SelectModelAsync), "complete", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            DebugLogger.LogCommand(nameof(SelectModelAsync), "failed", error: $"{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            throw;
        }
#endif
    }

    /// <summary>
    /// Toggles the auto-accept setting. On success, publishes a <see cref="ProjectPreferenceChangedMessage"/>.
    /// On failure, reverts <see cref="AutoAccept"/> to its previous value and sets <see cref="ErrorMessage"/>.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    [RelayCommand]
    private async Task ToggleAutoAcceptAsync(CancellationToken ct)
    {
#if DEBUG
        var sw = Stopwatch.StartNew();
        DebugLogger.LogCommand(nameof(ToggleAutoAcceptAsync), "start");
        try
        {
#endif
        if (_currentProjectId is null)
            return;

        var previousValue = AutoAccept;
        var newValue = !AutoAccept;
        var projectId = _currentProjectId;

        _isTogglingAutoAccept = true;
        AutoAccept = newValue;

        try
        {
            var success = await _preferenceService
                .SetAutoAcceptAsync(projectId, newValue, ct)
                ;

            if (success)
            {
                await PublishChangedMessageAsync(projectId);
            }
            else
            {
                AutoAccept = previousValue;
                ErrorMessage = "Failed to save auto-accept preference.";
            }
        }
        finally
        {
            _isTogglingAutoAccept = false;
        }
#if DEBUG
        sw.Stop();
        DebugLogger.LogCommand(nameof(ToggleAutoAcceptAsync), "complete", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            DebugLogger.LogCommand(nameof(ToggleAutoAcceptAsync), "failed", error: $"{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            throw;
        }
#endif
    }

    /// <summary>
    /// Changes the thinking level. The property change triggers auto-save
    /// via <see cref="OnThinkingLevelChanged"/>.
    /// </summary>
    /// <param name="level">The new thinking level.</param>
    [RelayCommand]
    private void ChangeThinkingLevel(ThinkingLevel level)
    {
#if DEBUG
        DebugLogger.LogCommand(nameof(ChangeThinkingLevel), "start");
#endif
        ThinkingLevel = level;
#if DEBUG
        DebugLogger.LogCommand(nameof(ChangeThinkingLevel), "complete");
#endif
    }

    /// <summary>
    /// Opens the subagent picker. On selection, attempts to dispatch the subagent invocation.
    /// Since the opencode API does not currently support subagent invocation, sets an informational error message.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    [RelayCommand(CanExecute = nameof(CanInvokeSubagent))]
    private async Task InvokeSubagentAsync(CancellationToken ct)
    {
#if DEBUG
        var sw = Stopwatch.StartNew();
        DebugLogger.LogCommand(nameof(InvokeSubagentAsync), "start");
        try
        {
#endif
        IsBusy = true;
        ErrorMessage = null;

        try
        {
            await _popupService.ShowSubagentPickerAsync(OnSubagentSelected, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ErrorMessage = "Failed to open subagent picker.";
            SentryHelper.CaptureException(ex, new Dictionary<string, object>
            {
                ["context"] = "ContextSheetViewModel.InvokeSubagentAsync",
            });
        }
        finally
        {
            IsBusy = false;
        }
#if DEBUG
        sw.Stop();
        DebugLogger.LogCommand(nameof(InvokeSubagentAsync), "complete", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            DebugLogger.LogCommand(nameof(InvokeSubagentAsync), "failed", error: $"{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            throw;
        }
#endif
    }

    /// <summary>Determines whether <see cref="InvokeSubagentCommand"/> can execute.</summary>
    /// <returns><c>true</c> if not busy and at least one subagent is available.</returns>
    private bool CanInvokeSubagent() => !IsBusy && _subagentAgents.Count > 0;

    /// <summary>
    /// Callback invoked when the user selects a subagent from the picker.
    /// Since the opencode API does not currently support subagent invocation,
    /// sets an informational error message.
    /// </summary>
    /// <param name="agentName">The selected subagent name.</param>
    private void OnSubagentSelected(string agentName)
    {
        // REQ-010 fallback: no API endpoint available for subagent invocation.
        ErrorMessage = "Subagent invocation not supported by this server version.";
    }

    /// <summary>
    /// Deletes the current session after user confirmation.
    /// Dismisses the Context Sheet and navigates to a new chat on success.
    /// Disabled while <see cref="IsBusy"/> is <c>true</c>.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    [RelayCommand(CanExecute = nameof(CanDeleteSession))]
    private async Task DeleteSessionAsync(CancellationToken ct)
    {
#if DEBUG
        var sw = Stopwatch.StartNew();
        DebugLogger.LogCommand(nameof(DeleteSessionAsync), "start");
        try
        {
#endif
        if (_currentSessionId is null || _currentProjectId is null)
            return;

        var confirmed = await _popupService.ShowConfirmDeleteAsync(
            "Delete session",
            "Are you sure you want to delete this session? This action cannot be undone.",
            ct);

        if (!confirmed)
            return;

        IsBusy = true;
        try
        {
            var deleted = await _sessionService.DeleteSessionAsync(_currentSessionId, ct)
                ;

            if (deleted)
            {
                WeakReferenceMessenger.Default.Send(
                    new SessionDeletedMessage(_currentSessionId, _currentProjectId));

                // Dismiss the sheet first, then navigate to a new chat
                await _popupService.PopPopupAsync(ct);
                await _navigationService.GoToAsync("//chat", new Dictionary<string, object>(), ct)
                    ;
            }
            else
            {
                ErrorMessage = "Failed to delete the session.";
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            SentryHelper.CaptureException(ex, new Dictionary<string, object>
            {
                ["context"] = "ContextSheetViewModel.DeleteSessionAsync",
                ["sessionId"] = _currentSessionId,
            });
            ErrorMessage = "Failed to delete the session.";
        }
        finally
        {
            IsBusy = false;
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

    /// <summary>Determines whether <see cref="DeleteSessionCommand"/> can execute.</summary>
    /// <returns><c>true</c> when not busy.</returns>
    private bool CanDeleteSession() => !IsBusy;

    /// <summary>
    /// Dismisses the Context Sheet by popping the modal navigation stack.
    /// Preferences are already auto-saved on every change; no save or rollback is performed.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    [RelayCommand]
    private async Task CloseAsync(CancellationToken ct)
    {
#if DEBUG
        var sw = Stopwatch.StartNew();
        DebugLogger.LogCommand(nameof(CloseAsync), "start");
        try
        {
#endif
        await _popupService.PopPopupAsync(ct);
#if DEBUG
        sw.Stop();
        DebugLogger.LogCommand(nameof(CloseAsync), "complete", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            DebugLogger.LogCommand(nameof(CloseAsync), "failed", error: $"{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            throw;
        }
#endif
    }

    // ─── Initialization ───────────────────────────────────────────────────────

    /// <summary>
    /// Loads preferences for the specified project and populates all observable properties.
    /// Must be called by the MAUI layer before the sheet is presented.
    /// </summary>
    /// <param name="projectId">The project identifier to load preferences for.</param>
    /// <param name="sessionId">The session identifier stored for use by <see cref="DeleteSessionCommand"/>.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task InitializeAsync(string projectId, string sessionId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectId);

        _isInitializing = true;
        IsBusy = true;

        try
        {
            _currentProjectId = projectId;
            _currentSessionId = sessionId;

            // Load project display name
            var project = await _projectService.GetProjectByIdAsync(projectId, ct);
            ProjectName = project is not null
                ? ProjectNameHelper.ExtractFromWorktree(project.Worktree)
                : "No project";

            // Load preferences — returns global defaults if no row exists for this project
            var pref = await _preferenceService.GetOrDefaultAsync(projectId, ct);

            SelectedAgentName = pref.AgentName;
            SelectedModelId = pref.DefaultModelId;
            ThinkingLevel = pref.ThinkingLevel;
            AutoAccept = pref.AutoAccept;
            ShowUnhandledSseEvents = pref.ShowUnhandledSseEvents;

            // Load subagent list for InvokeSubagentCommand CanExecute evaluation [REQ-009]
            _subagentAgents = await _agentService.GetSubagentAgentsAsync(ct);
            InvokeSubagentCommand.NotifyCanExecuteChanged();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            SentryHelper.CaptureException(ex, new Dictionary<string, object>
            {
                ["context"] = "ContextSheetViewModel.InitializeAsync",
                ["projectId"] = projectId,
            });
        }
        finally
        {
            _isInitializing = false;
            IsBusy = false;
        }
    }

    // ─── Auto-save via CommunityToolkit partial On*Changed methods ────────────

    /// <summary>
    /// Invoked by the CommunityToolkit source generator when <see cref="SelectedAgentName"/> changes.
    /// Skipped during <see cref="InitializeAsync"/> via the <c>_isInitializing</c> guard.
    /// </summary>
    partial void OnSelectedAgentNameChanged(string? value)
    {
        if (_isInitializing || _currentProjectId is null)
            return;

        _ = SaveAgentAsync(value);
    }

    /// <summary>
    /// Invoked by the CommunityToolkit source generator when <see cref="SelectedModelId"/> changes.
    /// Skipped during <see cref="InitializeAsync"/> via the <c>_isInitializing</c> guard.
    /// </summary>
    partial void OnSelectedModelIdChanged(string? value)
    {
        if (_isInitializing || _currentProjectId is null)
            return;

        _ = SaveModelAsync(value);
    }

    /// <summary>
    /// Invoked by the CommunityToolkit source generator when <see cref="ThinkingLevel"/> changes.
    /// Skipped during <see cref="InitializeAsync"/> via the <c>_isInitializing</c> guard.
    /// </summary>
    partial void OnThinkingLevelChanged(ThinkingLevel value)
    {
        if (_isInitializing || _currentProjectId is null)
            return;

        _ = SaveThinkingLevelAsync(value);
    }

    /// <summary>
    /// Invoked by the CommunityToolkit source generator when <see cref="AutoAccept"/> changes.
    /// Skipped during <see cref="InitializeAsync"/> via the <c>_isInitializing</c> guard.
    /// Skipped during <see cref="ToggleAutoAcceptCommand"/> execution (including rollback) via the <c>_isTogglingAutoAccept</c> guard.
    /// </summary>
    partial void OnAutoAcceptChanged(bool value)
    {
        if (_isInitializing || _isTogglingAutoAccept || _currentProjectId is null)
            return;

        _ = SaveAutoAcceptAsync(value);
    }

    /// <summary>
    /// Invoked by the CommunityToolkit source generator when <see cref="ShowUnhandledSseEvents"/> changes.
    /// Skipped during <see cref="InitializeAsync"/> via the <c>_isInitializing</c> guard.
    /// </summary>
    partial void OnShowUnhandledSseEventsChanged(bool value)
    {
        if (_isInitializing || _currentProjectId is null)
            return;

        _ = SaveShowUnhandledSseEventsAsync(value);
    }

    // ─── Private save helpers ─────────────────────────────────────────────────

    /// <summary>Persists the agent name and publishes a change message on success.</summary>
    private async Task SaveAgentAsync(string? agentName)
    {
        var projectId = _currentProjectId!;
        var success = await _preferenceService
            .SetAgentAsync(projectId, agentName, CancellationToken.None)
            ;

        if (success)
            await PublishChangedMessageAsync(projectId);
        else
            ErrorMessage = "Failed to save agent preference.";
    }

    /// <summary>Persists the model ID (or clears it if null) and publishes a change message on success.</summary>
    private async Task SaveModelAsync(string? modelId)
    {
        var projectId = _currentProjectId!;
        bool success;

        if (modelId is not null)
        {
            success = await _preferenceService
                .SetDefaultModelAsync(projectId, modelId, CancellationToken.None)
                ;
        }
        else
        {
            success = await _preferenceService
                .ClearDefaultModelAsync(projectId, CancellationToken.None)
                ;
        }

        if (success)
            await PublishChangedMessageAsync(projectId);
        else
            ErrorMessage = "Failed to save model preference.";
    }

    /// <summary>Persists the thinking level and publishes a change message on success.</summary>
    private async Task SaveThinkingLevelAsync(ThinkingLevel level)
    {
        var projectId = _currentProjectId!;
        var success = await _preferenceService
            .SetThinkingLevelAsync(projectId, level, CancellationToken.None)
            ;

        if (success)
            await PublishChangedMessageAsync(projectId);
        else
            ErrorMessage = "Failed to save thinking level preference.";
    }

    /// <summary>Persists the auto-accept setting and publishes a change message on success.</summary>
    private async Task SaveAutoAcceptAsync(bool autoAccept)
    {
        var projectId = _currentProjectId!;
        var success = await _preferenceService
            .SetAutoAcceptAsync(projectId, autoAccept, CancellationToken.None)
            ;

        if (success)
            await PublishChangedMessageAsync(projectId);
        else
            ErrorMessage = "Failed to save auto-accept preference.";
    }

    /// <summary>Persists the show-unhandled-SSE-events setting and publishes a change message on success.</summary>
    private async Task SaveShowUnhandledSseEventsAsync(bool value)
    {
        var projectId = _currentProjectId!;
        var success = await _preferenceService
            .SetShowUnhandledSseEventsAsync(projectId, value, CancellationToken.None)
            ;

        if (success)
            await PublishChangedMessageAsync(projectId);
        else
            ErrorMessage = "Failed to save show-unhandled-events preference.";
    }

    /// <summary>
    /// Loads the latest preference state and publishes a <see cref="ProjectPreferenceChangedMessage"/>
    /// so subscribers (e.g., <see cref="ChatViewModel"/>) can update their state.
    /// </summary>
    private async Task PublishChangedMessageAsync(string projectId)
    {
        var updatedPref = await _preferenceService
            .GetOrDefaultAsync(projectId, CancellationToken.None)
            ;

        WeakReferenceMessenger.Default.Send(
            new ProjectPreferenceChangedMessage(projectId, updatedPref));
    }
}
