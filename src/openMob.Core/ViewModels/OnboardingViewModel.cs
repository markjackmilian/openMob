using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using openMob.Core.Data.Repositories;
using openMob.Core.Infrastructure.Dtos;
using openMob.Core.Infrastructure.Http;
using openMob.Core.Infrastructure.Http.Dtos.Opencode;
using openMob.Core.Infrastructure.Monitoring;
using openMob.Core.Infrastructure.Security;
using openMob.Core.Services;

namespace openMob.Core.ViewModels;

/// <summary>
/// ViewModel for the OnboardingPage. Manages a 5-step linear onboarding flow
/// with progress tracking, server connection setup, and optional provider configuration.
/// </summary>
/// <remarks>
/// <para>Steps:</para>
/// <list type="number">
///   <item>Welcome — informational, always allows advancing.</item>
///   <item>Connect Server — requires successful connection test before advancing (REQ-008).</item>
///   <item>Provider Setup — optional, can be skipped (REQ-009).</item>
///   <item>Permissions — informational.</item>
///   <item>Completion — navigates to ChatPage (REQ-010).</item>
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
    /// <param name="providerService">Service for provider operations.</param>
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

    /// <summary>Gets or sets the current onboarding step (1-based, range 1–5).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Progress))]
    [NotifyPropertyChangedFor(nameof(CanGoNext))]
    [NotifyPropertyChangedFor(nameof(CanGoBack))]
    [NotifyPropertyChangedFor(nameof(IsStepOptional))]
    private int _currentStep = 1;

    /// <summary>Gets the total number of onboarding steps.</summary>
    public int TotalSteps => 5;

    /// <summary>Gets the progress as a fraction (0.0 to 1.0) for the ProgressBar.</summary>
    public double Progress => CurrentStep / (double)TotalSteps;

    /// <summary>Gets whether the "Next" button should be enabled for the current step.</summary>
    public bool CanGoNext => CurrentStep switch
    {
        2 => IsConnectionSuccessful,
        _ => true,
    };

    /// <summary>Gets whether the "Back" button should be visible.</summary>
    public bool CanGoBack => CurrentStep > 1;

    /// <summary>Gets whether the current step is optional (can be skipped).</summary>
    public bool IsStepOptional => CurrentStep == 3;

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

    // ─── Step 3: Provider setup ───────────────────────────────────────────────

    /// <summary>Gets or sets the list of available providers loaded from the server.</summary>
    [ObservableProperty]
    private ObservableCollection<ProviderDto> _providers = [];

    /// <summary>Gets or sets the ID of the currently selected provider.</summary>
    [ObservableProperty]
    private string? _selectedProviderId;

    /// <summary>Gets or sets the API key entered for the selected provider.</summary>
    [ObservableProperty]
    private string _providerApiKey = string.Empty;

    // ─── Saved connection ID (for credential storage) ─────────────────────────

    /// <summary>The ID of the server connection created during Step 2.</summary>
    private string? _savedConnectionId;

    // ─── Commands ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Advances to the next onboarding step. At step 5, completes the onboarding.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    [RelayCommand]
    private async Task NextStepAsync(CancellationToken ct)
    {
        if (CurrentStep == 3 && !string.IsNullOrWhiteSpace(SelectedProviderId) && !string.IsNullOrWhiteSpace(ProviderApiKey))
        {
            // Save provider API key before advancing
            await _providerService.SetProviderAuthAsync(SelectedProviderId, ProviderApiKey, ct);
        }

        if (CurrentStep >= TotalSteps)
        {
            await CompleteOnboardingAsync(ct);
            return;
        }

        CurrentStep++;

        // Load step-specific data
        if (CurrentStep == 3)
        {
            await LoadProvidersAsync(ct);
        }
    }

    /// <summary>Goes back to the previous onboarding step.</summary>
    [RelayCommand]
    private void PreviousStep()
    {
        if (CurrentStep > 1)
            CurrentStep--;
    }

    /// <summary>Skips the current optional step and advances to the next one.</summary>
    /// <param name="ct">Cancellation token.</param>
    [RelayCommand]
    private async Task SkipStepAsync(CancellationToken ct)
    {
        if (CurrentStep >= TotalSteps)
        {
            await CompleteOnboardingAsync(ct);
            return;
        }

        CurrentStep++;

        if (CurrentStep == 3)
        {
            await LoadProvidersAsync(ct);
        }
    }

    /// <summary>
    /// Tests the server connection using the entered URL and token (REQ-008).
    /// On success, creates a <see cref="ServerConnection"/> entity and saves credentials.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    [RelayCommand]
    private async Task TestConnectionAsync(CancellationToken ct)
    {
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
            var port = uri.Port > 0 ? uri.Port : 4096;

            // Create or update the server connection
            var connectionDto = new ServerConnectionDto(
                Id: string.Empty, // Will be generated by repository
                Name: $"{host}:{port}",
                Host: host,
                Port: port,
                Username: "opencode",
                IsActive: false,
                DiscoveredViaMdns: false,
                CreatedAt: DateTime.UtcNow,
                UpdatedAt: DateTime.UtcNow,
                HasPassword: !string.IsNullOrWhiteSpace(ServerToken));

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
    }

    /// <summary>
    /// Completes the onboarding and navigates to the ChatPage (REQ-010).
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    [RelayCommand]
    private async Task CompleteOnboardingAsync(CancellationToken ct)
    {
        SentryHelper.AddBreadcrumb("Onboarding completed — navigating to chat", "onboarding");
        await _navigationService.GoToAsync("//chat", ct);
    }

    // ─── Private helpers ──────────────────────────────────────────────────────

    /// <summary>Loads available providers from the server for Step 3.</summary>
    /// <param name="ct">Cancellation token.</param>
    private async Task LoadProvidersAsync(CancellationToken ct)
    {
        try
        {
            var providers = await _providerService.GetProvidersAsync(ct);
            Providers = new ObservableCollection<ProviderDto>(providers);
        }
        catch (Exception ex)
        {
            SentryHelper.CaptureException(ex, new Dictionary<string, object>
            {
                ["context"] = "OnboardingViewModel.LoadProvidersAsync",
            });
            Providers = [];
        }
    }
}
