using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using openMob.Core.Data.Entities;
using openMob.Core.Helpers;
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

    /// <summary>Tracks the current project ID for save operations.</summary>
    private string? _currentProjectId;

    /// <summary>
    /// Prevents auto-save partial methods from firing during <see cref="InitializeAsync"/>.
    /// Set to true at the start of initialization, false when complete.
    /// </summary>
    private bool _isInitializing;

    /// <summary>Initialises the ContextSheetViewModel with required dependencies.</summary>
    /// <param name="projectService">Service for project operations.</param>
    /// <param name="preferenceService">Service for per-project preference persistence.</param>
    /// <param name="popupService">Service for opening picker popups from the Core layer.</param>
    public ContextSheetViewModel(
        IProjectService projectService,
        IProjectPreferenceService preferenceService,
        IAppPopupService popupService)
    {
        ArgumentNullException.ThrowIfNull(projectService);
        ArgumentNullException.ThrowIfNull(preferenceService);
        ArgumentNullException.ThrowIfNull(popupService);

        _projectService = projectService;
        _preferenceService = preferenceService;
        _popupService = popupService;
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
    /// Returns "Default" when <see cref="SelectedAgentName"/> is null.
    /// </summary>
    public string SelectedAgentDisplayName => SelectedAgentName ?? "Default";

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

    /// <summary>Gets or sets whether the sheet is currently loading preferences.</summary>
    [ObservableProperty]
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
        await _popupService.ShowAgentPickerAsync(agentName =>
        {
            SelectedAgentName = agentName;
        }, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Opens the model picker popup and updates <see cref="SelectedModelId"/> on selection.
    /// The property change triggers auto-save via <see cref="OnSelectedModelIdChanged"/>.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    [RelayCommand]
    private async Task SelectModelAsync(CancellationToken ct)
    {
        await _popupService.ShowModelPickerAsync(modelId =>
        {
            SelectedModelId = modelId;
        }, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Changes the thinking level. The property change triggers auto-save
    /// via <see cref="OnThinkingLevelChanged"/>.
    /// </summary>
    /// <param name="level">The new thinking level.</param>
    [RelayCommand]
    private void ChangeThinkingLevel(ThinkingLevel level)
    {
        ThinkingLevel = level;
    }

    /// <summary>
    /// Opens the agent picker in subagent invocation mode.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    [RelayCommand]
    private async Task InvokeSubagentAsync(CancellationToken ct)
    {
        await _popupService.ShowAgentPickerSubagentModeAsync(ct).ConfigureAwait(false);
    }

    // ─── Initialization ───────────────────────────────────────────────────────

    /// <summary>
    /// Loads preferences for the specified project and populates all observable properties.
    /// Must be called by the MAUI layer before the sheet is presented.
    /// </summary>
    /// <param name="projectId">The project identifier to load preferences for.</param>
    /// <param name="sessionId">The session identifier (reserved for future session-level overrides; currently unused).</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task InitializeAsync(string projectId, string sessionId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectId);

        _isInitializing = true;
        IsBusy = true;

        try
        {
            _currentProjectId = projectId;

            // Load project display name
            var project = await _projectService.GetProjectByIdAsync(projectId, ct).ConfigureAwait(false);
            ProjectName = project is not null
                ? ProjectNameHelper.ExtractFromWorktree(project.Worktree)
                : "No project";

            // Load preferences — returns global defaults if no row exists for this project
            var pref = await _preferenceService.GetOrDefaultAsync(projectId, ct).ConfigureAwait(false);

            SelectedAgentName = pref.AgentName;
            SelectedModelId = pref.DefaultModelId;
            ThinkingLevel = pref.ThinkingLevel;
            AutoAccept = pref.AutoAccept;
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
    /// </summary>
    partial void OnAutoAcceptChanged(bool value)
    {
        if (_isInitializing || _currentProjectId is null)
            return;

        _ = SaveAutoAcceptAsync(value);
    }

    // ─── Private save helpers ─────────────────────────────────────────────────

    /// <summary>Persists the agent name and publishes a change message on success.</summary>
    private async Task SaveAgentAsync(string? agentName)
    {
        var projectId = _currentProjectId!;
        var success = await _preferenceService
            .SetAgentAsync(projectId, agentName, CancellationToken.None)
            .ConfigureAwait(false);

        if (success)
            await PublishChangedMessageAsync(projectId).ConfigureAwait(false);
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
                .ConfigureAwait(false);
        }
        else
        {
            success = await _preferenceService
                .ClearDefaultModelAsync(projectId, CancellationToken.None)
                .ConfigureAwait(false);
        }

        if (success)
            await PublishChangedMessageAsync(projectId).ConfigureAwait(false);
        else
            ErrorMessage = "Failed to save model preference.";
    }

    /// <summary>Persists the thinking level and publishes a change message on success.</summary>
    private async Task SaveThinkingLevelAsync(ThinkingLevel level)
    {
        var projectId = _currentProjectId!;
        var success = await _preferenceService
            .SetThinkingLevelAsync(projectId, level, CancellationToken.None)
            .ConfigureAwait(false);

        if (success)
            await PublishChangedMessageAsync(projectId).ConfigureAwait(false);
        else
            ErrorMessage = "Failed to save thinking level preference.";
    }

    /// <summary>Persists the auto-accept setting and publishes a change message on success.</summary>
    private async Task SaveAutoAcceptAsync(bool autoAccept)
    {
        var projectId = _currentProjectId!;
        var success = await _preferenceService
            .SetAutoAcceptAsync(projectId, autoAccept, CancellationToken.None)
            .ConfigureAwait(false);

        if (success)
            await PublishChangedMessageAsync(projectId).ConfigureAwait(false);
        else
            ErrorMessage = "Failed to save auto-accept preference.";
    }

    /// <summary>
    /// Loads the latest preference state and publishes a <see cref="ProjectPreferenceChangedMessage"/>
    /// so subscribers (e.g., <see cref="ChatViewModel"/>) can update their state.
    /// </summary>
    private async Task PublishChangedMessageAsync(string projectId)
    {
        var updatedPref = await _preferenceService
            .GetOrDefaultAsync(projectId, CancellationToken.None)
            .ConfigureAwait(false);

        WeakReferenceMessenger.Default.Send(
            new ProjectPreferenceChangedMessage(projectId, updatedPref));
    }
}
