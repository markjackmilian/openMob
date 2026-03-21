using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using openMob.Core.Data.Repositories;
using openMob.Core.Infrastructure.Discovery;
using openMob.Core.Infrastructure.Dtos;
using openMob.Core.Infrastructure.Logging;
using openMob.Core.Infrastructure.Monitoring;
using openMob.Core.Services;

namespace openMob.Core.ViewModels;

/// <summary>
/// ViewModel for the Server Management page.
/// </summary>
/// <remarks>
/// <para>
/// Displays all saved server connections and discovered servers on the local network.
/// Provides commands to navigate to the detail page (Add or Edit mode), trigger an mDNS scan,
/// and refresh the list on page appearance.
/// </para>
/// <para>
/// All external dependencies are injected via constructor — no MAUI dependencies.
/// </para>
/// </remarks>
public sealed partial class ServerManagementViewModel : ObservableObject
{
    private readonly IServerConnectionRepository _serverConnectionRepository;
    private readonly IOpencodeDiscoveryService _discoveryService;
    private readonly INavigationService _navigationService;

    /// <summary>
    /// Initialises the <see cref="ServerManagementViewModel"/> with required dependencies.
    /// </summary>
    /// <param name="serverConnectionRepository">Repository for server connection CRUD operations.</param>
    /// <param name="discoveryService">Service for mDNS server discovery.</param>
    /// <param name="navigationService">Service for Shell navigation.</param>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is <see langword="null"/>.</exception>
    public ServerManagementViewModel(
        IServerConnectionRepository serverConnectionRepository,
        IOpencodeDiscoveryService discoveryService,
        INavigationService navigationService)
    {
        ArgumentNullException.ThrowIfNull(serverConnectionRepository);
        ArgumentNullException.ThrowIfNull(discoveryService);
        ArgumentNullException.ThrowIfNull(navigationService);

        _serverConnectionRepository = serverConnectionRepository;
        _discoveryService = discoveryService;
        _navigationService = navigationService;
    }

    // ─── Observable state ─────────────────────────────────────────────────────

    /// <summary>Gets or sets the collection of saved server connections.</summary>
    [ObservableProperty]
    private ObservableCollection<ServerConnectionDto> _servers = [];

    /// <summary>Gets or sets the collection of servers discovered via mDNS in the last scan.</summary>
    [ObservableProperty]
    private ObservableCollection<DiscoveredServerDto> _discoveredServers = [];

    /// <summary>Gets or sets whether the saved server list is currently loading.</summary>
    [ObservableProperty]
    private bool _isLoading;

    /// <summary>Gets or sets whether an mDNS scan is currently in progress.</summary>
    [ObservableProperty]
    private bool _isScanning;

    /// <summary>Gets or sets whether the most recent mDNS scan has completed.</summary>
    [ObservableProperty]
    private bool _scanCompleted;

    /// <summary>Gets or sets the error message from the last failed <see cref="LoadAsync"/> call. <c>null</c> when no error has occurred.</summary>
    [ObservableProperty]
    private string? _loadError;

    /// <summary>Gets whether any servers were discovered in the last scan.</summary>
    public bool HasDiscoveredServers => DiscoveredServers.Count > 0;

    // ─── Commands ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Loads all saved server connections from the repository and resets the discovered servers list.
    /// Called on page appearance and after any add/edit/delete/activate action.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    [RelayCommand]
    private async Task LoadAsync(CancellationToken ct)
    {
#if DEBUG
        var sw = Stopwatch.StartNew();
        DebugLogger.LogCommand(nameof(LoadAsync), "start");
        try
        {
#endif
        IsLoading = true;
        LoadError = null;

        try
        {
            var servers = await _serverConnectionRepository.GetAllAsync(ct);
            Servers = new ObservableCollection<ServerConnectionDto>(servers);
            DiscoveredServers = [];
            ScanCompleted = false;
        }
        catch (Exception ex)
        {
            LoadError = "Could not load servers. Please try again.";
            SentryHelper.CaptureException(ex, new Dictionary<string, object>
            {
                ["context"] = "ServerManagementViewModel.LoadAsync",
            });
        }
        finally
        {
            IsLoading = false;
        }
#if DEBUG
        sw.Stop();
        DebugLogger.LogCommand(nameof(LoadAsync), "complete", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            DebugLogger.LogCommand(nameof(LoadAsync), "failed", error: $"{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            throw;
        }
#endif
    }

    /// <summary>
    /// Starts an mDNS scan and progressively populates <see cref="DiscoveredServers"/>.
    /// Servers already present in <see cref="Servers"/> (matched by Host and Port) are excluded.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    [RelayCommand]
    private async Task ScanAsync(CancellationToken ct)
    {
#if DEBUG
        var sw = Stopwatch.StartNew();
        DebugLogger.LogCommand(nameof(ScanAsync), "start");
        try
        {
#endif
        IsScanning = true;
        ScanCompleted = false;
        DiscoveredServers = [];
        OnPropertyChanged(nameof(HasDiscoveredServers));

        try
        {
            await foreach (var discovered in _discoveryService.ScanAsync(ct))
            {
                // Deduplicate: skip if any saved server has the same Host and Port.
                var alreadySaved = Servers.Any(s =>
                    string.Equals(s.Host, discovered.Host, StringComparison.OrdinalIgnoreCase)
                    && s.Port == discovered.Port);

                if (!alreadySaved)
                {
                    DiscoveredServers.Add(discovered);
                    OnPropertyChanged(nameof(HasDiscoveredServers));
                }
            }
        }
        catch (Exception ex)
        {
            SentryHelper.CaptureException(ex, new Dictionary<string, object>
            {
                ["context"] = "ServerManagementViewModel.ScanAsync",
            });
        }
        finally
        {
            IsScanning = false;
            ScanCompleted = true;
            OnPropertyChanged(nameof(HasDiscoveredServers));
        }
#if DEBUG
        sw.Stop();
        DebugLogger.LogCommand(nameof(ScanAsync), "complete", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            DebugLogger.LogCommand(nameof(ScanAsync), "failed", error: $"{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            throw;
        }
#endif
    }

    /// <summary>
    /// Navigates to the Server Detail page in Add mode (no pre-populated data).
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    [RelayCommand]
    private async Task NavigateToAddAsync(CancellationToken ct)
    {
#if DEBUG
        var sw = Stopwatch.StartNew();
        DebugLogger.LogCommand(nameof(NavigateToAddAsync), "start");
        try
        {
#endif
        await _navigationService.GoToAsync("server-detail", ct);
#if DEBUG
        sw.Stop();
        DebugLogger.LogCommand(nameof(NavigateToAddAsync), "complete", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            DebugLogger.LogCommand(nameof(NavigateToAddAsync), "failed", error: $"{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            throw;
        }
#endif
    }

    /// <summary>
    /// Navigates to the Server Detail page in Edit mode, pre-populated with the given server's data.
    /// </summary>
    /// <param name="dto">The saved server connection to edit.</param>
    /// <param name="ct">Cancellation token.</param>
    [RelayCommand]
    private async Task NavigateToEditAsync(ServerConnectionDto dto, CancellationToken ct)
    {
#if DEBUG
        var sw = Stopwatch.StartNew();
        DebugLogger.LogCommand(nameof(NavigateToEditAsync), "start");
        try
        {
#endif
        await _navigationService.GoToAsync("server-detail", new Dictionary<string, object>
        {
            ["serverId"] = dto.Id,
        }, ct);
#if DEBUG
        sw.Stop();
        DebugLogger.LogCommand(nameof(NavigateToEditAsync), "complete", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            DebugLogger.LogCommand(nameof(NavigateToEditAsync), "failed", error: $"{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            throw;
        }
#endif
    }

    /// <summary>
    /// Navigates to the Server Detail page in Add mode, pre-populated with data from a discovered server.
    /// </summary>
    /// <param name="dto">The discovered server to pre-populate from.</param>
    /// <param name="ct">Cancellation token.</param>
    [RelayCommand]
    private async Task NavigateToDiscoveredAsync(DiscoveredServerDto dto, CancellationToken ct)
    {
#if DEBUG
        var sw = Stopwatch.StartNew();
        DebugLogger.LogCommand(nameof(NavigateToDiscoveredAsync), "start");
        try
        {
#endif
        await _navigationService.GoToAsync("server-detail", new Dictionary<string, object>
        {
            ["discoveredHost"] = dto.Host,
            ["discoveredPort"] = dto.Port.ToString(),
            ["discoveredName"] = dto.Name,
        }, ct);
#if DEBUG
        sw.Stop();
        DebugLogger.LogCommand(nameof(NavigateToDiscoveredAsync), "complete", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            DebugLogger.LogCommand(nameof(NavigateToDiscoveredAsync), "failed", error: $"{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            throw;
        }
#endif
    }
}
