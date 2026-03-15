using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using openMob.Core.Data.Repositories;
using openMob.Core.Infrastructure.Http;
using openMob.Core.Infrastructure.Monitoring;
using openMob.Core.Services;

namespace openMob.Core.ViewModels;

/// <summary>
/// ViewModel for the SplashPage. Executes bootstrap logic to determine the initial
/// navigation destination based on server configuration and session state.
/// </summary>
/// <remarks>
/// <para>Routing logic (REQ-001 through REQ-006):</para>
/// <list type="number">
///   <item>No server configured → OnboardingPage</item>
///   <item>Server configured but unreachable (5s timeout) → ChatPage (offline mode)</item>
///   <item>Server reachable, no sessions → ChatPage (new empty session)</item>
///   <item>Server reachable, sessions exist → ChatPage (last session)</item>
/// </list>
/// <para>
/// Navigation uses absolute routes (<c>"//route"</c>) to prevent back navigation to splash (REQ-006).
/// </para>
/// </remarks>
public sealed partial class SplashViewModel : ObservableObject
{
    private readonly IServerConnectionRepository _serverConnectionRepository;
    private readonly IOpencodeConnectionManager _connectionManager;
    private readonly ISessionService _sessionService;
    private readonly INavigationService _navigationService;

    /// <summary>Initialises the SplashViewModel with required dependencies.</summary>
    /// <param name="serverConnectionRepository">Repository for server connection data.</param>
    /// <param name="connectionManager">Manager for checking server reachability.</param>
    /// <param name="sessionService">Service for session operations.</param>
    /// <param name="navigationService">Service for Shell navigation.</param>
    public SplashViewModel(
        IServerConnectionRepository serverConnectionRepository,
        IOpencodeConnectionManager connectionManager,
        ISessionService sessionService,
        INavigationService navigationService)
    {
        ArgumentNullException.ThrowIfNull(serverConnectionRepository);
        ArgumentNullException.ThrowIfNull(connectionManager);
        ArgumentNullException.ThrowIfNull(sessionService);
        ArgumentNullException.ThrowIfNull(navigationService);

        _serverConnectionRepository = serverConnectionRepository;
        _connectionManager = connectionManager;
        _sessionService = sessionService;
        _navigationService = navigationService;
    }

    /// <summary>Gets or sets whether the bootstrap process is in progress.</summary>
    [ObservableProperty]
    private bool _isLoading;

    /// <summary>
    /// Executes the bootstrap logic and navigates to the appropriate destination.
    /// This command is invoked once when the SplashPage appears.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    [RelayCommand]
    private async Task InitializeAsync(CancellationToken ct)
    {
        if (IsLoading)
            return;

        IsLoading = true;

        try
        {
            SentryHelper.AddBreadcrumb("Splash bootstrap started", "navigation");

            // Step 1: Check if any server is configured (REQ-002)
            var activeConnection = await _serverConnectionRepository.GetActiveAsync(ct);

            if (activeConnection is null)
            {
                SentryHelper.AddBreadcrumb("No server configured — navigating to onboarding", "navigation");
                await _navigationService.GoToAsync("//onboarding", ct);
                return;
            }

            // Step 2: Check if server is reachable with 5s timeout (REQ-003)
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(5));

            bool isReachable;
            try
            {
                isReachable = await _connectionManager.IsServerReachableAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                // Timeout expired but the original token was not cancelled — server is unreachable
                isReachable = false;
            }

            if (!isReachable)
            {
                SentryHelper.AddBreadcrumb("Server unreachable — navigating to chat (offline mode)", "navigation");
                await _navigationService.GoToAsync("//chat", ct);
                return;
            }

            // Step 3: Check for existing sessions (REQ-004, REQ-005)
            var sessions = await _sessionService.GetAllSessionsAsync(ct);

            if (sessions.Count > 0)
            {
                // Navigate to chat with the most recently updated session
                var lastSession = sessions
                    .OrderByDescending(s => s.Time.Updated)
                    .First();

                SentryHelper.AddBreadcrumb(
                    $"Sessions found — navigating to chat with session '{lastSession.Id}'",
                    "navigation");

                await _navigationService.GoToAsync("//chat", new Dictionary<string, object>
                {
                    ["sessionId"] = lastSession.Id,
                }, ct);
            }
            else
            {
                SentryHelper.AddBreadcrumb("No sessions found — navigating to chat (new session)", "navigation");
                await _navigationService.GoToAsync("//chat", ct);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // App shutdown or page dismissed — do nothing
        }
        catch (Exception ex)
        {
            SentryHelper.CaptureException(ex, new Dictionary<string, object>
            {
                ["context"] = "SplashViewModel.InitializeAsync",
            });

            // Fallback: navigate to chat even on unexpected errors
            try
            {
                await _navigationService.GoToAsync("//chat", ct);
            }
            catch
            {
                // Last resort — nothing more we can do
            }
        }
        finally
        {
            IsLoading = false;
        }
    }
}
