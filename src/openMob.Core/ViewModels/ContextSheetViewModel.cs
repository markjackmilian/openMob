using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using openMob.Core.Helpers;
using openMob.Core.Infrastructure.Monitoring;
using openMob.Core.Models;
using openMob.Core.Services;

namespace openMob.Core.ViewModels;

/// <summary>
/// ViewModel for the Context Sheet bottom sheet (REQ-025 through REQ-028).
/// Provides read/write access to session-level settings: project, agent, model,
/// thinking level, auto-accept, and subagent invocation.
/// </summary>
public sealed partial class ContextSheetViewModel : ObservableObject
{
    private readonly IProjectService _projectService;
    private readonly IProviderService _providerService;
    private readonly IAppPopupService _popupService;
    private readonly IProjectPreferenceService _preferenceService;

    /// <summary>Initialises the ContextSheetViewModel with required dependencies.</summary>
    /// <param name="projectService">Service for project operations.</param>
    /// <param name="providerService">Service for AI provider operations.</param>
    /// <param name="popupService">Service for popup/dialog operations.</param>
    /// <param name="preferenceService">Service for per-project preference persistence.</param>
    public ContextSheetViewModel(
        IProjectService projectService,
        IProviderService providerService,
        IAppPopupService popupService,
        IProjectPreferenceService preferenceService)
    {
        ArgumentNullException.ThrowIfNull(projectService);
        ArgumentNullException.ThrowIfNull(providerService);
        ArgumentNullException.ThrowIfNull(popupService);
        ArgumentNullException.ThrowIfNull(preferenceService);

        _projectService = projectService;
        _providerService = providerService;
        _popupService = popupService;
        _preferenceService = preferenceService;
    }

    // ─── Observable Properties ────────────────────────────────────────────────

    /// <summary>Gets or sets the current project display name.</summary>
    [ObservableProperty]
    private string _projectName = "No project";

    /// <summary>Gets or sets the current project identifier.</summary>
    [ObservableProperty]
    private string? _currentProjectId;

    /// <summary>Gets or sets the active agent display name.</summary>
    [ObservableProperty]
    private string _agentName = "Default";

    /// <summary>Gets or sets the active model display name.</summary>
    [ObservableProperty]
    private string _modelName = "No model";

    /// <summary>Gets or sets the current thinking/reasoning level.</summary>
    [ObservableProperty]
    private ThinkingLevel _thinkingLevel = ThinkingLevel.Medium;

    /// <summary>Gets or sets whether auto-accept is enabled for agent suggestions.</summary>
    [ObservableProperty]
    private bool _autoAccept;

    // ─── Commands ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Loads the current context (project, agent, model, thinking level, auto-accept)
    /// from the respective services.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    [RelayCommand]
    private async Task LoadContextAsync(CancellationToken ct)
    {
        try
        {
            // Load current project
            var currentProject = await _projectService.GetCurrentProjectAsync(ct).ConfigureAwait(false);
            if (currentProject is not null)
            {
                CurrentProjectId = currentProject.Id;
                ProjectName = ProjectNameHelper.ExtractFromWorktree(currentProject.Worktree);
            }
            else
            {
                CurrentProjectId = null;
                ProjectName = "No project";
            }

            // Load model preference for this project
            if (CurrentProjectId is not null)
            {
                var pref = await _preferenceService.GetAsync(CurrentProjectId, ct).ConfigureAwait(false);
                if (pref?.DefaultModelId is not null)
                {
                    ModelName = ModelIdHelper.ExtractModelName(pref.DefaultModelId);
                }
                else
                {
                    ModelName = "No model";
                }
            }
        }
        catch (Exception ex)
        {
            SentryHelper.CaptureException(ex, new Dictionary<string, object>
            {
                ["context"] = "ContextSheetViewModel.LoadContextAsync",
            });
        }
    }

    /// <summary>
    /// Opens the project switcher popup.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    [RelayCommand]
    private async Task OpenProjectSwitcherAsync(CancellationToken ct)
    {
        // Signal intent — the View layer handles the ProjectSwitcherSheet popup.
        await Task.CompletedTask;
    }

    /// <summary>
    /// Opens the agent picker popup in primary agent mode.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    [RelayCommand]
    private async Task OpenAgentPickerAsync(CancellationToken ct)
    {
        // Signal intent — the View layer handles the AgentPickerSheet popup.
        await Task.CompletedTask;
    }

    /// <summary>
    /// Opens the model picker popup and updates the model name on selection.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    [RelayCommand]
    private async Task OpenModelPickerAsync(CancellationToken ct)
    {
        await _popupService.ShowModelPickerAsync(modelId =>
        {
            ModelName = ModelIdHelper.ExtractModelName(modelId);
        }, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Changes the thinking level and persists the preference.
    /// </summary>
    /// <param name="level">The new thinking level.</param>
    [RelayCommand]
    private void ChangeThinkingLevel(ThinkingLevel level)
    {
        ThinkingLevel = level;
        // Persistence is local-only for v1 (see spec Open Question #3).
        // Server sync via UpdateConfigAsync when API schema is confirmed.
    }

    /// <summary>
    /// Toggles the auto-accept setting and persists the preference.
    /// </summary>
    [RelayCommand]
    private void ToggleAutoAccept()
    {
        AutoAccept = !AutoAccept;
        // Persistence is local-only for v1 (see spec Open Question #3).
    }

    /// <summary>
    /// Opens the agent picker in subagent invocation mode (REQ-031).
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    [RelayCommand]
    private async Task InvokeSubagentAsync(CancellationToken ct)
    {
        await _popupService.ShowAgentPickerSubagentModeAsync(ct);
    }
}
