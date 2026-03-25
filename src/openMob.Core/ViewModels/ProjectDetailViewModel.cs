using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using openMob.Core.Data.Entities;
using openMob.Core.Helpers;
using openMob.Core.Infrastructure.Http;
using openMob.Core.Infrastructure.Http.Dtos.Opencode;
using openMob.Core.Infrastructure.Logging;
using openMob.Core.Infrastructure.Monitoring;
using openMob.Core.Services;

namespace openMob.Core.ViewModels;

/// <summary>
/// ViewModel for the project detail bottom sheet.
/// Loads the active project metadata, server path/VCS/config data, and per-project
/// preference overrides, then exposes a read-only summary for the UI.
/// </summary>
/// <remarks>
/// Registered as Transient because the sheet is opened on demand and should not retain
/// stale state between invocations.
/// </remarks>
public sealed partial class ProjectDetailViewModel : ObservableObject
{
    private readonly IProjectService _projectService;
    private readonly IOpencodeApiClient _apiClient;
    private readonly IProjectPreferenceService _preferenceService;
    private readonly IAppPopupService _popupService;
    private readonly IDispatcherService _dispatcher;

    /// <summary>Tracks the current project identifier for preference updates.</summary>
    private string? _currentProjectId;

    /// <summary>Tracks the server default model loaded from <c>GET /config</c>.</summary>
    private string? _serverDefaultModelId;

    /// <summary>Initialises the project detail ViewModel with required dependencies.</summary>
    /// <param name="projectService">Service for project metadata.</param>
    /// <param name="apiClient">Direct opencode API client for path, VCS, and config data.</param>
    /// <param name="preferenceService">Service for per-project preference persistence.</param>
    /// <param name="popupService">Service for opening nested popups.</param>
    /// <param name="dispatcher">UI dispatcher for thread-safe property updates.</param>
    public ProjectDetailViewModel(
        IProjectService projectService,
        IOpencodeApiClient apiClient,
        IProjectPreferenceService preferenceService,
        IAppPopupService popupService,
        IDispatcherService dispatcher)
    {
        ArgumentNullException.ThrowIfNull(projectService);
        ArgumentNullException.ThrowIfNull(apiClient);
        ArgumentNullException.ThrowIfNull(preferenceService);
        ArgumentNullException.ThrowIfNull(popupService);
        ArgumentNullException.ThrowIfNull(dispatcher);

        _projectService = projectService;
        _apiClient = apiClient;
        _preferenceService = preferenceService;
        _popupService = popupService;
        _dispatcher = dispatcher;
    }

    // ─── Observable properties ────────────────────────────────────────────────

    /// <summary>Gets or sets whether the sheet is loading data.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ChangeModelCommand))]
    [NotifyCanExecuteChangedFor(nameof(ResetModelCommand))]
    private bool _isLoading;

    /// <summary>Gets or sets the display name of the current project.</summary>
    [ObservableProperty]
    private string? _projectName;

    /// <summary>Gets or sets the full worktree path.</summary>
    [ObservableProperty]
    private string? _worktreePath;

    /// <summary>Gets or sets the detected VCS type.</summary>
    [ObservableProperty]
    private string? _vcsType;

    /// <summary>Gets or sets the active git branch.</summary>
    [ObservableProperty]
    private string? _gitBranch;

    /// <summary>Gets or sets the working directory path.</summary>
    [ObservableProperty]
    private string? _workingDirectory;

    /// <summary>Gets or sets the config directory path.</summary>
    [ObservableProperty]
    private string? _configPath;

    /// <summary>Gets or sets the created-at timestamp formatted for display.</summary>
    [ObservableProperty]
    private string? _createdAt;

    /// <summary>Gets or sets the initialized-at timestamp formatted for display.</summary>
    [ObservableProperty]
    private string? _initializedAt;

    /// <summary>Gets or sets the effective model identifier.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ResetModelCommand))]
    private string? _effectiveModelId;

    /// <summary>Gets or sets whether a local model override is active.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ResetModelCommand))]
    private bool _isModelOverridden;

    /// <summary>Gets or sets the label describing the model source.</summary>
    [ObservableProperty]
    private string _modelSourceLabel = "Server default";

    // ─── Commands ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Opens the existing model picker popup so the user can change the project default model.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    [RelayCommand(CanExecute = nameof(CanChangeModel))]
    private async Task ChangeModelAsync(CancellationToken ct)
    {
#if DEBUG
        var sw = Stopwatch.StartNew();
        DebugLogger.LogCommand(nameof(ChangeModelAsync), "start");
        try
        {
#endif
        if (string.IsNullOrWhiteSpace(_currentProjectId))
            return;

        await _popupService.ShowModelPickerAsync(selectedModelId =>
        {
            _ = HandleModelSelectedAsync(selectedModelId);
        }, ct);
#if DEBUG
        sw.Stop();
        DebugLogger.LogCommand(nameof(ChangeModelAsync), "complete", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            DebugLogger.LogCommand(nameof(ChangeModelAsync), "failed", error: $"{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            throw;
        }
#endif
    }

    /// <summary>
    /// Resets the local model override so the server default becomes effective again.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    [RelayCommand(CanExecute = nameof(CanResetModel))]
    private async Task ResetModelAsync(CancellationToken ct)
    {
#if DEBUG
        var sw = Stopwatch.StartNew();
        DebugLogger.LogCommand(nameof(ResetModelAsync), "start");
        try
        {
#endif
        if (string.IsNullOrWhiteSpace(_currentProjectId))
            return;

        try
        {
            var cleared = await _preferenceService.ClearDefaultModelAsync(_currentProjectId, ct);
            if (!cleared)
                return;

            _dispatcher.Dispatch(() =>
            {
                EffectiveModelId = _serverDefaultModelId;
                IsModelOverridden = false;
                ModelSourceLabel = "Server default";
            });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            SentryHelper.CaptureException(ex, new Dictionary<string, object>
            {
                ["context"] = "ProjectDetailViewModel.ResetModelAsync",
                ["projectId"] = _currentProjectId,
            });
        }
#if DEBUG
        sw.Stop();
        DebugLogger.LogCommand(nameof(ResetModelAsync), "complete", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            DebugLogger.LogCommand(nameof(ResetModelAsync), "failed", error: $"{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            throw;
        }
#endif
    }

    /// <summary>Determines whether the model picker can be opened.</summary>
    /// <returns><c>true</c> when the sheet is initialized and not loading.</returns>
    private bool CanChangeModel() => !IsLoading && !string.IsNullOrWhiteSpace(_currentProjectId);

    /// <summary>Determines whether the reset action can be executed.</summary>
    /// <returns><c>true</c> when a local model override is active and the sheet is initialized.</returns>
    private bool CanResetModel() => !IsLoading && IsModelOverridden && !string.IsNullOrWhiteSpace(_currentProjectId);

    // ─── Initialization ───────────────────────────────────────────────────────

    /// <summary>
    /// Loads the project detail data for the specified project and updates the observable state.
    /// </summary>
    /// <param name="projectId">The project identifier to load.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task InitializeAsync(string projectId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectId);

        _currentProjectId = projectId;
        _dispatcher.Dispatch(() => IsLoading = true);

        try
        {
            var projectTask = LoadProjectAsync(projectId, ct);
            var vcsTask = LoadVcsInfoAsync(ct);
            var pathTask = LoadPathAsync(ct);
            var configTask = LoadConfigAsync(ct);
            var preferenceTask = LoadPreferenceAsync(projectId, ct);

            await Task.WhenAll(projectTask, vcsTask, pathTask, configTask, preferenceTask).ConfigureAwait(false);

            var project = await projectTask.ConfigureAwait(false);
            var vcsInfo = await vcsTask.ConfigureAwait(false);
            var path = await pathTask.ConfigureAwait(false);
            var config = await configTask.ConfigureAwait(false);
            var preference = await preferenceTask.ConfigureAwait(false);

            var projectName = project is not null ? ProjectNameHelper.ExtractFromWorktree(project.Worktree) : null;
            var worktreePath = project?.Worktree;
            var vcsType = project?.Vcs;
            var gitBranch = vcsInfo?.Branch;
            var workingDirectory = path?.Directory;
            var configPath = path?.Config;
            var createdAt = project is not null ? FormatUnixTime(project.Time.Created) : null;
            var initializedAt = project?.Time.Initialized is not null ? FormatUnixTime(project.Time.Initialized.Value) : null;
            var serverDefaultModelId = config?.Model;
            var effectiveModelId = !string.IsNullOrWhiteSpace(preference?.DefaultModelId)
                ? preference!.DefaultModelId
                : serverDefaultModelId;
            var isModelOverridden = !string.IsNullOrWhiteSpace(preference?.DefaultModelId);
            var modelSourceLabel = isModelOverridden ? "Project override" : "Server default";

            _dispatcher.Dispatch(() =>
            {
                ProjectName = projectName;
                WorktreePath = worktreePath;
                VcsType = vcsType;
                GitBranch = gitBranch;
                WorkingDirectory = workingDirectory;
                ConfigPath = configPath;
                CreatedAt = createdAt;
                InitializedAt = initializedAt;
                _serverDefaultModelId = serverDefaultModelId;
                EffectiveModelId = effectiveModelId;
                IsModelOverridden = isModelOverridden;
                ModelSourceLabel = modelSourceLabel;
                ChangeModelCommand.NotifyCanExecuteChanged();
                ResetModelCommand.NotifyCanExecuteChanged();
            });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            SentryHelper.CaptureException(ex, new Dictionary<string, object>
            {
                ["context"] = "ProjectDetailViewModel.InitializeAsync",
                ["projectId"] = projectId,
            });
        }
        finally
        {
            _dispatcher.Dispatch(() => IsLoading = false);
        }
    }

    /// <summary>Loads project metadata while capturing unexpected failures.</summary>
    private async Task<ProjectDto?> LoadProjectAsync(string projectId, CancellationToken ct)
    {
        try
        {
            return await _projectService.GetProjectByIdAsync(projectId, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            SentryHelper.CaptureException(ex, new Dictionary<string, object>
            {
                ["context"] = "ProjectDetailViewModel.LoadProjectAsync",
                ["projectId"] = projectId,
            });
            return null;
        }
    }

    /// <summary>Loads VCS information while capturing unexpected failures.</summary>
    private async Task<VcsInfoDto?> LoadVcsInfoAsync(CancellationToken ct)
    {
        try
        {
            var result = await _apiClient.GetVcsInfoAsync(ct).ConfigureAwait(false);
            return result.IsSuccess ? result.Value : null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            SentryHelper.CaptureException(ex, new Dictionary<string, object>
            {
                ["context"] = "ProjectDetailViewModel.LoadVcsInfoAsync",
            });
            return null;
        }
    }

    /// <summary>Loads path information while capturing unexpected failures.</summary>
    private async Task<PathDto?> LoadPathAsync(CancellationToken ct)
    {
        try
        {
            var result = await _apiClient.GetPathAsync(ct).ConfigureAwait(false);
            return result.IsSuccess ? result.Value : null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            SentryHelper.CaptureException(ex, new Dictionary<string, object>
            {
                ["context"] = "ProjectDetailViewModel.LoadPathAsync",
            });
            return null;
        }
    }

    /// <summary>Loads config information while capturing unexpected failures.</summary>
    private async Task<ConfigDto?> LoadConfigAsync(CancellationToken ct)
    {
        try
        {
            var result = await _apiClient.GetConfigAsync(ct).ConfigureAwait(false);
            return result.IsSuccess ? result.Value : null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            SentryHelper.CaptureException(ex, new Dictionary<string, object>
            {
                ["context"] = "ProjectDetailViewModel.LoadConfigAsync",
            });
            return null;
        }
    }

    /// <summary>Loads project preference data while capturing unexpected failures.</summary>
    private async Task<ProjectPreference?> LoadPreferenceAsync(string projectId, CancellationToken ct)
    {
        try
        {
            return await _preferenceService.GetOrDefaultAsync(projectId, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            SentryHelper.CaptureException(ex, new Dictionary<string, object>
            {
                ["context"] = "ProjectDetailViewModel.LoadPreferenceAsync",
                ["projectId"] = projectId,
            });
            return null;
        }
    }

    /// <summary>Handles a selected model from the popup picker.</summary>
    /// <param name="modelId">The selected model identifier.</param>
    private async Task HandleModelSelectedAsync(string modelId)
    {
        if (string.IsNullOrWhiteSpace(modelId) || string.IsNullOrWhiteSpace(_currentProjectId))
            return;

        try
        {
            var saved = await _preferenceService.SetDefaultModelAsync(_currentProjectId, modelId, CancellationToken.None);
            if (!saved)
                return;

            _dispatcher.Dispatch(() =>
            {
                EffectiveModelId = modelId;
                IsModelOverridden = true;
                ModelSourceLabel = "Project override";
            });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            SentryHelper.CaptureException(ex, new Dictionary<string, object>
            {
                ["context"] = "ProjectDetailViewModel.HandleModelSelectedAsync",
                ["projectId"] = _currentProjectId,
                ["modelId"] = modelId,
            });
        }
    }

    /// <summary>Formats a Unix timestamp in milliseconds for display.</summary>
    /// <param name="unixMilliseconds">The timestamp in Unix milliseconds.</param>
    /// <returns>A localized short date/time string.</returns>
    private static string FormatUnixTime(long unixMilliseconds) =>
        DateTimeOffset.FromUnixTimeMilliseconds(unixMilliseconds).ToLocalTime().ToString("g");
}
