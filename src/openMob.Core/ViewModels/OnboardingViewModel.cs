using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using openMob.Core.Data.Repositories;
using openMob.Core.Infrastructure.Dtos;
using openMob.Core.Infrastructure.Http;
using openMob.Core.Infrastructure.Http.Dtos.Opencode;
using openMob.Core.Infrastructure.Logging;
using openMob.Core.Infrastructure.Monitoring;
using openMob.Core.Infrastructure.Security;
using openMob.Core.Models;
using openMob.Core.Services;

namespace openMob.Core.ViewModels;

/// <summary>
/// ViewModel for the OnboardingPage. Manages a 4-step linear onboarding flow
/// with progress tracking, server connection setup, and default model selection.
/// </summary>
/// <remarks>
/// <para>Steps:</para>
/// <list type="number">
///   <item>Welcome — informational, always allows advancing.</item>
///   <item>Connect Server — requires successful connection test before advancing.</item>
///   <item>Default Model Selection — requires a model to be selected before advancing (REQ-005, REQ-006).</item>
///   <item>Completion — navigates to ChatPage.</item>
/// </list>
/// </remarks>
public sealed partial class OnboardingViewModel : ObservableObject
{
    private readonly IServerConnectionRepository _serverConnectionRepository;
    private readonly IServerCredentialStore _credentialStore;
    private readonly IOpencodeConnectionManager _connectionManager;
    private readonly IOpencodeApiClient _apiClient;
    private readonly IProviderService _providerService;
    private readonly INavigationService _navigationService;
    private readonly IAppPopupService _popupService;

    /// <summary>Initialises the OnboardingViewModel with required dependencies.</summary>
    /// <param name="serverConnectionRepository">Repository for server connection CRUD.</param>
    /// <param name="credentialStore">Secure storage for server credentials.</param>
    /// <param name="connectionManager">Manager for server connectivity.</param>
    /// <param name="apiClient">The opencode API client for health checks.</param>
    /// <param name="providerService">Service for provider and model operations.</param>
    /// <param name="navigationService">Service for Shell navigation.</param>
    /// <param name="popupService">Service for popup/dialog operations.</param>
    public OnboardingViewModel(
        IServerConnectionRepository serverConnectionRepository,
        IServerCredentialStore credentialStore,
        IOpencodeConnectionManager connectionManager,
        IOpencodeApiClient apiClient,
        IProviderService providerService,
        INavigationService navigationService,
        IAppPopupService popupService)
    {
        ArgumentNullException.ThrowIfNull(serverConnectionRepository);
        ArgumentNullException.ThrowIfNull(credentialStore);
        ArgumentNullException.ThrowIfNull(connectionManager);
        ArgumentNullException.ThrowIfNull(apiClient);
        ArgumentNullException.ThrowIfNull(providerService);
        ArgumentNullException.ThrowIfNull(navigationService);
        ArgumentNullException.ThrowIfNull(popupService);

        _serverConnectionRepository = serverConnectionRepository;
        _credentialStore = credentialStore;
        _connectionManager = connectionManager;
        _apiClient = apiClient;
        _providerService = providerService;
        _navigationService = navigationService;
        _popupService = popupService;
    }

    // ─── Step tracking ────────────────────────────────────────────────────────

    /// <summary>Gets or sets the current onboarding step (1-based, range 1–4).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Progress))]
    [NotifyPropertyChangedFor(nameof(CanGoNext))]
    [NotifyPropertyChangedFor(nameof(CanGoBack))]
    [NotifyPropertyChangedFor(nameof(IsStepOptional))]
    private int _currentStep = 1;

    /// <summary>Gets the total number of onboarding steps.</summary>
    public int TotalSteps => 4;

    /// <summary>Gets the progress as a fraction (0.0 to 1.0) for the ProgressBar.</summary>
    public double Progress => CurrentStep / (double)TotalSteps;

    /// <summary>Gets whether the "Next" button should be enabled for the current step.</summary>
    public bool CanGoNext => CurrentStep switch
    {
        2 => IsConnectionSuccessful,
        3 => SelectedModelId is not null && !IsLoadingModels && ModelLoadError is null,
        _ => true,
    };

    /// <summary>Gets whether the "Back" button should be visible.</summary>
    public bool CanGoBack => CurrentStep > 1;

    /// <summary>Gets whether the current step is optional (can be skipped).</summary>
    /// <remarks>Only step 2 (server connection) is skippable. Step 3 (model selection) is mandatory (REQ-006).</remarks>
    public bool IsStepOptional => CurrentStep is 2;

    // ─── Step 2: Server connection ────────────────────────────────────────────

    /// <summary>Gets or sets the server URL entered by the user.</summary>
    [ObservableProperty]
    private string _serverUrl = string.Empty;

    /// <summary>Gets or sets the server access token entered by the user.</summary>
    [ObservableProperty]
    private string _serverToken = string.Empty;

    /// <summary>Gets or sets whether a connection test has been performed.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanGoNext))]
    private bool _isConnectionTested;

    /// <summary>Gets or sets whether the last connection test was successful.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanGoNext))]
    private bool _isConnectionSuccessful;

    /// <summary>Gets or sets the status message from the last connection test.</summary>
    [ObservableProperty]
    private string _connectionStatusMessage = string.Empty;

    /// <summary>Gets or sets whether a connection test is currently in progress.</summary>
    [ObservableProperty]
    private bool _isTestingConnection;

    // ─── Step 3: Default model selection ──────────────────────────────────────

    /// <summary>Gets or sets the list of available models loaded from the server (REQ-003).</summary>
    [ObservableProperty]
    private ObservableCollection<ModelItem> _availableModels = [];

    /// <summary>Gets or sets the ID of the selected default model (REQ-005).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanGoNext))]
    private string? _selectedModelId;

    /// <summary>Gets or sets whether models are currently being loaded from the server.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanGoNext))]
    private bool _isLoadingModels;

    /// <summary>Gets or sets the error message if model loading failed (REQ-004), or null on success.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanGoNext))]
    private string? _modelLoadError;

    // ─── Saved connection ID (for credential storage) ─────────────────────────

    /// <summary>The ID of the server connection created during Step 2.</summary>
    private string? _savedConnectionId;

    // ─── Commands ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Advances to the next onboarding step. At step 4, completes the onboarding.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    [RelayCommand]
    private async Task NextStepAsync(CancellationToken ct)
    {
#if DEBUG
        var sw = Stopwatch.StartNew();
        DebugLogger.LogCommand(nameof(NextStepAsync), "start");
        try
        {
#endif
        if (CurrentStep >= TotalSteps)
        {
            await CompleteOnboardingAsync(ct);
            return;
        }

        CurrentStep++;

        // Load step-specific data
        if (CurrentStep == 3)
        {
            await LoadModelsAsync(ct);
        }
#if DEBUG
        sw.Stop();
        DebugLogger.LogCommand(nameof(NextStepAsync), "complete", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            DebugLogger.LogCommand(nameof(NextStepAsync), "failed", error: $"{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            throw;
        }
#endif
    }

    /// <summary>Goes back to the previous onboarding step.</summary>
    [RelayCommand]
    private void PreviousStep()
    {
#if DEBUG
        DebugLogger.LogCommand(nameof(PreviousStep), "start");
#endif
        if (CurrentStep > 1)
            CurrentStep--;
#if DEBUG
        DebugLogger.LogCommand(nameof(PreviousStep), "complete");
#endif
    }

    /// <summary>Skips the current optional step and advances to the next one.</summary>
    /// <param name="ct">Cancellation token.</param>
    [RelayCommand]
    private async Task SkipStepAsync(CancellationToken ct)
    {
#if DEBUG
        var sw = Stopwatch.StartNew();
        DebugLogger.LogCommand(nameof(SkipStepAsync), "start");
        try
        {
#endif
        if (CurrentStep >= TotalSteps)
        {
            await CompleteOnboardingAsync(ct);
            return;
        }

        CurrentStep++;

        if (CurrentStep == 3)
        {
            await LoadModelsAsync(ct);
        }
#if DEBUG
        sw.Stop();
        DebugLogger.LogCommand(nameof(SkipStepAsync), "complete", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            DebugLogger.LogCommand(nameof(SkipStepAsync), "failed", error: $"{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            throw;
        }
#endif
    }

    /// <summary>
    /// Tests the server connection using the entered URL and token.
    /// On success, creates a <see cref="ServerConnection"/> entity and saves credentials.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    [RelayCommand]
    private async Task TestConnectionAsync(CancellationToken ct)
    {
#if DEBUG
        var sw = Stopwatch.StartNew();
        DebugLogger.LogCommand(nameof(TestConnectionAsync), "start");
        try
        {
#endif
        if (IsTestingConnection)
            return;

        IsTestingConnection = true;
        IsConnectionTested = false;
        IsConnectionSuccessful = false;
        ConnectionStatusMessage = string.Empty;

        try
        {
            // Validate inputs
            if (string.IsNullOrWhiteSpace(ServerUrl))
            {
                ConnectionStatusMessage = "Please enter a server URL.";
                IsConnectionTested = true;
                return;
            }

            // Parse URL to extract host and port
            if (!Uri.TryCreate(ServerUrl.Trim(), UriKind.Absolute, out var uri)
                || (uri.Scheme != "http" && uri.Scheme != "https"))
            {
                ConnectionStatusMessage = "Invalid URL format. Use http:// or https://";
                IsConnectionTested = true;
                return;
            }

            var host = uri.Host;
            var useHttps = uri.Scheme == "https";
            var defaultPort = useHttps ? 443 : 80;
            // Preserve the port exactly as specified in the URL.
            // If the URL uses the protocol's default port (e.g. https://host:443 or https://host),
            // store that default port — GetBaseUrlAsync will omit it when building the URL.
            var port = uri.IsDefaultPort ? defaultPort : uri.Port;

            // Create or update the server connection
            var connectionDto = new ServerConnectionDto(
                Id: string.Empty,
                Name: uri.IsDefaultPort ? host : $"{host}:{uri.Port}",
                Host: host,
                Port: port,
                Username: "opencode",
                IsActive: false,
                DiscoveredViaMdns: false,
                UseHttps: useHttps,
                CreatedAt: DateTime.UtcNow,
                UpdatedAt: DateTime.UtcNow,
                HasPassword: !string.IsNullOrWhiteSpace(ServerToken),
                DefaultModelId: null);

            var savedConnection = await _serverConnectionRepository.AddAsync(connectionDto, ct);
            _savedConnectionId = savedConnection.Id;

            // Save token as password in secure storage
            if (!string.IsNullOrWhiteSpace(ServerToken))
            {
                await _credentialStore.SavePasswordAsync(savedConnection.Id, ServerToken.Trim(), ct);
            }

            // Set as active connection
            await _serverConnectionRepository.SetActiveAsync(savedConnection.Id, ct);

            // Test the connection via health check
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(10));

            var healthResult = await _apiClient.GetHealthAsync(timeoutCts.Token);

            if (healthResult.IsSuccess && healthResult.Value is not null && healthResult.Value.Healthy)
            {
                IsConnectionSuccessful = true;
                ConnectionStatusMessage = $"Connected — server v{healthResult.Value.Version}";
                SentryHelper.AddBreadcrumb("Onboarding: server connection test succeeded", "onboarding");
            }
            else
            {
                IsConnectionSuccessful = false;
                var errorMessage = healthResult.Error?.Message ?? "Server returned unhealthy status.";
                ConnectionStatusMessage = $"Connection failed: {errorMessage}";

                // Clean up the failed connection
                await _serverConnectionRepository.DeleteAsync(savedConnection.Id, ct);
                _savedConnectionId = null;
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            IsConnectionSuccessful = false;
            ConnectionStatusMessage = "Connection timed out. Please check the URL and try again.";
        }
        catch (Exception ex)
        {
            IsConnectionSuccessful = false;
            ConnectionStatusMessage = $"Unexpected error: {ex.Message}";
            SentryHelper.CaptureException(ex, new Dictionary<string, object>
            {
                ["context"] = "OnboardingViewModel.TestConnectionAsync",
                ["serverUrl"] = ServerUrl,
            });
        }
        finally
        {
            IsConnectionTested = true;
            IsTestingConnection = false;
        }

        if (!IsConnectionSuccessful && IsConnectionTested)
        {
            await _popupService.ShowErrorAsync("Connection Failed", ConnectionStatusMessage, ct);
        }
#if DEBUG
        sw.Stop();
        DebugLogger.LogCommand(nameof(TestConnectionAsync), "complete", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            DebugLogger.LogCommand(nameof(TestConnectionAsync), "failed", error: $"{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            throw;
        }
#endif
    }

    /// <summary>
    /// Selects a model from the available models list and updates the visual selection state.
    /// </summary>
    /// <param name="modelId">The ID of the model to select (e.g. <c>"anthropic/claude-3-opus"</c>).</param>
    [RelayCommand]
    private void SelectModel(string modelId)
    {
#if DEBUG
        DebugLogger.LogCommand(nameof(SelectModel), "start");
#endif
        SelectedModelId = modelId;

        // Rebuild the collection with updated IsSelected state
        var updatedModels = AvailableModels.Select(m => m with { IsSelected = m.Id == modelId }).ToList();
        AvailableModels = new ObservableCollection<ModelItem>(updatedModels);

        SentryHelper.AddBreadcrumb($"Onboarding: model selected — {modelId}", "onboarding");
#if DEBUG
        DebugLogger.LogCommand(nameof(SelectModel), "complete");
#endif
    }

    /// <summary>
    /// Completes the onboarding, saves the default model, and navigates to the ChatPage.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    [RelayCommand]
    private async Task CompleteOnboardingAsync(CancellationToken ct)
    {
#if DEBUG
        var sw = Stopwatch.StartNew();
        DebugLogger.LogCommand(nameof(CompleteOnboardingAsync), "start");
        try
        {
#endif
        // Save the selected default model to the server connection (REQ-007)
        if (!string.IsNullOrEmpty(SelectedModelId) && !string.IsNullOrEmpty(_savedConnectionId))
        {
            await _serverConnectionRepository.SetDefaultModelAsync(_savedConnectionId, SelectedModelId, ct);
        }

        SentryHelper.AddBreadcrumb("Onboarding completed — navigating to chat", "onboarding");
        await _navigationService.GoToAsync("//chat", ct);
#if DEBUG
        sw.Stop();
        DebugLogger.LogCommand(nameof(CompleteOnboardingAsync), "complete", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            DebugLogger.LogCommand(nameof(CompleteOnboardingAsync), "failed", error: $"{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            throw;
        }
#endif
    }

    // ─── Private helpers ──────────────────────────────────────────────────────

    /// <summary>
    /// Loads available models from the server for Step 3 (REQ-003).
    /// Uses <see cref="IProviderService.GetConfiguredProvidersAsync"/> and extracts models
    /// from each provider's <c>Models</c> JsonElement (same pattern as <c>ModelPickerViewModel</c>).
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    private async Task LoadModelsAsync(CancellationToken ct)
    {
#if DEBUG
        var sw = Stopwatch.StartNew();
        DebugLogger.LogCommand(nameof(LoadModelsAsync), "start");
#endif
        IsLoadingModels = true;
        ModelLoadError = null;
        AvailableModels = [];
        SelectedModelId = null;

        try
        {
            var providers = await _providerService.GetConfiguredProvidersAsync(ct);

            var allModels = new List<ModelItem>();

            foreach (var provider in providers)
            {
                var models = ExtractModelsFromProvider(provider.Id, provider.Name, provider.Models);
                allModels.AddRange(models);
            }

            AvailableModels = new ObservableCollection<ModelItem>(allModels);

            if (allModels.Count == 0)
            {
                ModelLoadError = "No models available. Please configure a provider on the server.";
            }
        }
        catch (Exception ex)
        {
            ModelLoadError = $"Failed to load models: {ex.Message}";
            SentryHelper.CaptureException(ex, new Dictionary<string, object>
            {
                ["context"] = "OnboardingViewModel.LoadModelsAsync",
            });
            AvailableModels = [];
        }
        finally
        {
            IsLoadingModels = false;
#if DEBUG
            sw.Stop();
            DebugLogger.LogCommand(nameof(LoadModelsAsync), "complete", sw.ElapsedMilliseconds);
#endif
        }
    }

    /// <summary>
    /// Extracts model items from a provider's <c>Models</c> JsonElement.
    /// The Models field is a raw JSON object where keys are model IDs and values
    /// contain model metadata.
    /// </summary>
    /// <param name="providerId">The provider identifier (used as prefix for model IDs).</param>
    /// <param name="providerName">The provider display name.</param>
    /// <param name="modelsJson">The raw JSON element containing model definitions.</param>
    /// <returns>A list of model items extracted from the JSON.</returns>
    private IReadOnlyList<ModelItem> ExtractModelsFromProvider(
        string providerId,
        string providerName,
        JsonElement modelsJson)
    {
        var models = new List<ModelItem>();

        try
        {
            if (modelsJson.ValueKind != JsonValueKind.Object)
                return models;

            foreach (var modelProperty in modelsJson.EnumerateObject())
            {
                var modelId = modelProperty.Name;
                var modelName = modelId; // Default to ID as name
                string? contextSize = null;

                // Try to extract display name from the model object
                if (modelProperty.Value.ValueKind == JsonValueKind.Object)
                {
                    if (modelProperty.Value.TryGetProperty("name", out var nameElement)
                        && nameElement.ValueKind == JsonValueKind.String)
                    {
                        modelName = nameElement.GetString() ?? modelId;
                    }

                    // Try to extract context window size from "limit.context"
                    // (server shape: "limit": { "context": 131072, "output": 32768 })
                    if (modelProperty.Value.TryGetProperty("limit", out var limitElement) &&
                        limitElement.TryGetProperty("context", out var contextElement) &&
                        contextElement.ValueKind == JsonValueKind.Number &&
                        contextElement.TryGetInt64(out var contextLength))
                    {
                        contextSize = contextLength >= 1000
                            ? $"{contextLength / 1000}k tokens"
                            : $"{contextLength} tokens";
                    }
                }

                var fullModelId = $"{providerId}/{modelId}";
                models.Add(new ModelItem(
                    Id: fullModelId,
                    Name: modelName,
                    ProviderName: providerName,
                    ContextSize: contextSize,
                    IsSelected: fullModelId == SelectedModelId));
            }
        }
        // Broad catch is intentional: ExtractModelsFromProvider parses untrusted JSON
        // from the server. Any unexpected shape (missing properties, wrong types, etc.)
        // must not crash the wizard — we log to Sentry and return whatever models were
        // successfully parsed so far.
        catch (Exception ex)
        {
            SentryHelper.CaptureException(ex, new Dictionary<string, object>
            {
                ["context"] = "OnboardingViewModel.ExtractModelsFromProvider",
                ["providerId"] = providerId,
            });
        }

        return models;
    }
}
