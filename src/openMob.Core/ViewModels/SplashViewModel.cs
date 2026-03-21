using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using openMob.Core.Data.Repositories;
using openMob.Core.Infrastructure.Http;
using openMob.Core.Infrastructure.Logging;
using openMob.Core.Infrastructure.Monitoring;
using openMob.Core.Services;

namespace openMob.Core.ViewModels;

/// <summary>
/// ViewModel for the SplashPage. Executes bootstrap logic to determine the initial
/// navigation destination based on server configuration and session state.
/// </summary>
/// <remarks>
/// <para>Routing logic:</para>
/// <list type="number">
///   <item>No server configured → OnboardingPage</item>
///   <item>Server configured but unreachable (5s timeout) → shows "Server non raggiungibile", waits 2s, navigates to ServerManagementPage</item>
///   <item>Server configured but returns false → shows "Errore di connessione al server", waits 2s, navigates to ServerManagementPage</item>
///   <item>Server reachable, no sessions → ChatPage (new empty session)</item>
///   <item>Server reachable, sessions exist → ChatPage (last session)</item>
/// </list>
/// <para>
/// Navigation uses absolute routes (<c>"//route"</c>) to prevent back navigation to splash.
/// </para>
/// </remarks>
public sealed partial class SplashViewModel : ObservableObject
{
    private readonly IServerConnectionRepository _serverConnectionRepository;
    private readonly IOpencodeConnectionManager _connectionManager;
    private readonly ISessionService _sessionService;
    private readonly INavigationService _navigationService;
    private readonly TimeProvider _timeProvider;

    /// <summary>Initialises the SplashViewModel with required dependencies.</summary>
    /// <param name="serverConnectionRepository">Repository for server connection data.</param>
    /// <param name="connectionManager">Manager for checking server reachability.</param>
    /// <param name="sessionService">Service for session operations.</param>
    /// <param name="navigationService">Service for Shell navigation.</param>
    /// <param name="timeProvider">
    /// Optional time provider used for the 2-second delay before navigating away on error.
    /// Defaults to <see cref="TimeProvider.System"/> when <c>null</c>.
    /// Inject a fake provider in unit tests to avoid real delays.
    /// </param>
    public SplashViewModel(
        IServerConnectionRepository serverConnectionRepository,
        IOpencodeConnectionManager connectionManager,
        ISessionService sessionService,
        INavigationService navigationService,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(serverConnectionRepository);
        ArgumentNullException.ThrowIfNull(connectionManager);
        ArgumentNullException.ThrowIfNull(sessionService);
        ArgumentNullException.ThrowIfNull(navigationService);

        _serverConnectionRepository = serverConnectionRepository;
        _connectionManager = connectionManager;
        _sessionService = sessionService;
        _navigationService = navigationService;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>Gets or sets whether the bootstrap process is in progress.</summary>
    [ObservableProperty]
    private bool _isLoading;

    /// <summary>Gets or sets the status message displayed below the spinner during bootstrap.</summary>
    /// <remarks>
    /// Set to <see cref="string.Empty"/> initially. Updated to a localised string when a
    /// connection attempt is in progress or when an error occurs. The SplashPage shows this
    /// label only when the value is non-empty (REQ-012).
    /// </remarks>
    [ObservableProperty]
    private string _statusMessage = string.Empty;

    /// <summary>
    /// Executes the bootstrap logic and navigates to the appropriate destination.
    /// This command is invoked once when the SplashPage appears.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    [RelayCommand]
    private async Task InitializeAsync(CancellationToken ct)
    {
#if DEBUG
        var sw = Stopwatch.StartNew();
        DebugLogger.LogCommand(nameof(InitializeAsync), "start");
        try
        {
#endif
        if (IsLoading)
            return;

        IsLoading = true;

        try
        {
            SentryHelper.AddBreadcrumb("Splash bootstrap started", "navigation");

            // Step 1: Check if any server is configured (REQ-001)
            var activeConnection = await _serverConnectionRepository.GetActiveAsync(ct);

            if (activeConnection is null)
            {
                SentryHelper.AddBreadcrumb("No server configured — navigating to onboarding", "navigation");
                await _navigationService.GoToAsync("//onboarding", ct);
                return;
            }

            // Step 2: Indicate connection attempt to the user (REQ-002)
            StatusMessage = "Connessione al server in corso…";

            // Step 3: Check if server is reachable with 5s timeout
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(5));

            bool isReachable;
            try
            {
                isReachable = await _connectionManager.IsServerReachableAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                // Timeout expired but the original token was not cancelled — server is unreachable (REQ-003)
                SentryHelper.AddBreadcrumb("Server timeout — navigating to server management", "navigation");
                StatusMessage = "Server non raggiungibile";

                // Wait 2s so the user can read the message, then navigate (REQ-006, REQ-007).
                // If ct is cancelled during the delay, OperationCanceledException propagates to
                // the outer catch (ct.IsCancellationRequested) and no navigation occurs — correct.
                await Task.Delay(TimeSpan.FromSeconds(2), _timeProvider, ct);
                await _navigationService.GoToAsync("//server-management", ct);
                return;
            }

            if (!isReachable)
            {
                // Server returned false — generic connection error (REQ-005, REQ-006, REQ-007)
                SentryHelper.AddBreadcrumb("Server unreachable — navigating to server management", "navigation");
                StatusMessage = "Errore di connessione al server";

                await Task.Delay(TimeSpan.FromSeconds(2), _timeProvider, ct);
                await _navigationService.GoToAsync("//server-management", ct);
                return;
            }

            // Step 4: Check for existing sessions
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

            // Fallback: show error message, wait 2s, then navigate to server management
            StatusMessage = "Errore di connessione al server";
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(2), _timeProvider, ct);
                await _navigationService.GoToAsync("//server-management", ct);
            }
            catch
            {
                // Last resort — nothing more we can do (e.g. ct cancelled during delay)
            }
        }
        finally
        {
            IsLoading = false;
        }
#if DEBUG
        sw.Stop();
        DebugLogger.LogCommand(nameof(InitializeAsync), "complete", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            DebugLogger.LogCommand(nameof(InitializeAsync), "failed", error: $"{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            throw;
        }
#endif
    }
}
