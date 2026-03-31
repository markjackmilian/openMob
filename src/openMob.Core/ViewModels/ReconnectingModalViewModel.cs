using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using openMob.Core.Infrastructure.Http;
using openMob.Core.Services;

namespace openMob.Core.ViewModels;

/// <summary>
/// ViewModel for the non-dismissible reconnection modal sheet.
/// Owns the exponential backoff retry loop and exposes status properties
/// for the UI to bind to.
/// </summary>
/// <remarks>
/// <para>
/// The reconnection loop runs via <see cref="StartReconnectionLoopAsync"/> and probes the server
/// using <see cref="IOpencodeConnectionManager.IsServerReachableAsync"/>. On success, it raises
/// <see cref="ReconnectionSucceeded"/> and returns. On failure it waits the next backoff delay
/// before retrying.
/// </para>
/// <para>
/// The "Gestisci server" button calls <see cref="NavigateToServerManagementCommand"/>, which
/// pops the popup and navigates to the server management page.
/// </para>
/// </remarks>
public sealed partial class ReconnectingModalViewModel : ObservableObject
{
    private readonly IOpencodeConnectionManager _connectionManager;
    private readonly INavigationService _navigationService;
    private readonly IAppPopupService _popupService;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initialises the ViewModel with required dependencies.
    /// </summary>
    /// <param name="connectionManager">Manages the opencode server connection.</param>
    /// <param name="navigationService">Service for Shell navigation.</param>
    /// <param name="popupService">Service for popup operations.</param>
    /// <param name="timeProvider">
    /// The time provider to use. Defaults to <see cref="TimeProvider.System"/> when <c>null</c>.
    /// </param>
    public ReconnectingModalViewModel(
        IOpencodeConnectionManager connectionManager,
        INavigationService navigationService,
        IAppPopupService popupService,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(connectionManager);
        ArgumentNullException.ThrowIfNull(navigationService);
        ArgumentNullException.ThrowIfNull(popupService);

        _connectionManager = connectionManager;
        _navigationService = navigationService;
        _popupService = popupService;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    // ─── Observable Properties ────────────────────────────────────────────────

    /// <summary>Gets or sets the current status message displayed in the modal.</summary>
    [ObservableProperty]
    private string _statusMessage = "Connessione al server persa. Tentativo di riconnessione in corso…";

    /// <summary>Gets or sets the current reconnection attempt number (1-based within the current cycle).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AttemptSummary))]
    private int _attemptNumber = 0;

    /// <summary>Gets or sets the total number of attempts per cycle.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AttemptSummary))]
    private int _totalAttempts = 3;

    /// <summary>Gets a formatted attempt summary string, e.g. "Tentativo 2 di 3".</summary>
    public string AttemptSummary => $"Tentativo {AttemptNumber} di {TotalAttempts}";

    /// <summary>Gets or sets whether a reconnection probe is currently in flight.</summary>
    [ObservableProperty]
    private bool _isReconnecting = false;

    // ─── Events ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Raised when a reconnection probe succeeds (server is reachable).
    /// The caller should close the modal and reset the health state to <c>Healthy</c>.
    /// </summary>
    public event Action? ReconnectionSucceeded;

    // ─── Commands ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Navigates to the Server Management page.
    /// Pops the reconnection modal first, then pushes ServerManagementPage onto the
    /// navigation stack using the <c>"server-management-push"</c> route so that the
    /// page's back button returns to ChatPage on both iOS and Android.
    /// </summary>
    /// <remarks>
    /// The <c>"server-management-push"</c> route is registered in <c>AppShell.xaml.cs</c>
    /// as a push route separate from the <c>"server-management"</c> ShellContent root
    /// declaration. This allows back navigation via <c>".."</c> on both platforms.
    /// </remarks>
    /// <param name="ct">Cancellation token.</param>
    [RelayCommand]
    private async Task NavigateToServerManagementAsync(CancellationToken ct)
    {
        await _popupService.PopPopupAsync(ct).ConfigureAwait(false);
        await _navigationService.GoToAsync("server-management-push", ct).ConfigureAwait(false);
    }

    // ─── Reconnection Loop ────────────────────────────────────────────────────

    /// <summary>
    /// Starts the exponential backoff reconnection loop.
    /// Probes the server at 5 s, 10 s, and 20 s intervals (cycling indefinitely until
    /// a probe succeeds or <paramref name="ct"/> is cancelled).
    /// </summary>
    /// <remarks>
    /// On success, raises <see cref="ReconnectionSucceeded"/> and returns.
    /// On <see cref="OperationCanceledException"/>, exits silently.
    /// </remarks>
    /// <param name="ct">Cancellation token.</param>
    public async Task StartReconnectionLoopAsync(CancellationToken ct)
    {
        int[] delays = [5_000, 10_000, 20_000];

        try
        {
            while (!ct.IsCancellationRequested)
            {
                for (var cycleAttempt = 0; cycleAttempt < delays.Length; cycleAttempt++)
                {
                    ct.ThrowIfCancellationRequested();

                    AttemptNumber = cycleAttempt + 1;
                    StatusMessage = $"Tentativo {AttemptNumber} di {TotalAttempts}";
                    IsReconnecting = true;

                    bool reachable = await _connectionManager.IsServerReachableAsync(ct).ConfigureAwait(false);

                    IsReconnecting = false;

                    if (reachable)
                    {
                        ReconnectionSucceeded?.Invoke();
                        return;
                    }

                    await Task.Delay(delays[cycleAttempt], ct).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when the modal is dismissed or the app is shutting down — exit silently.
        }
    }
}
