using System.Diagnostics;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using openMob.Core.Data.Repositories;
using openMob.Core.Infrastructure.Dtos;
using openMob.Core.Infrastructure.Helpers;
using openMob.Core.Infrastructure.Http;
using openMob.Core.Infrastructure.Logging;
using openMob.Core.Infrastructure.Monitoring;
using openMob.Core.Infrastructure.Security;
using openMob.Core.Services;

namespace openMob.Core.ViewModels;

/// <summary>
/// ViewModel for the Server Detail page.
/// </summary>
/// <remarks>
/// <para>
/// Handles both Add mode (new server) and Edit mode (existing server).
/// In Add mode all fields are empty (or pre-populated from mDNS discovery).
/// In Edit mode the form is pre-populated from the repository and the Delete button is visible.
/// </para>
/// <para>
/// Call <see cref="InitialiseAsync"/> after construction to set the correct mode and
/// pre-populate the form fields.
/// </para>
/// <para>
/// All external dependencies are injected via constructor — no MAUI dependencies.
/// </para>
/// </remarks>
public sealed partial class ServerDetailViewModel : ObservableObject
{
    private readonly IServerConnectionRepository _serverConnectionRepository;
    private readonly IServerCredentialStore _credentialStore;
    private readonly IOpencodeConnectionManager _connectionManager;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly INavigationService _navigationService;
    private readonly IAppPopupService _popupService;
    private readonly IProviderService _providerService;

    // ─── Private non-observable state ─────────────────────────────────────────

    /// <summary>The ID of the server connection after a successful save (or loaded in Edit mode).</summary>
    private string? _savedServerId;

    /// <summary>Whether the server had a password stored at the time the form was loaded.</summary>
    private bool _originalHasPassword;

    /// <summary>
    /// Initialises the <see cref="ServerDetailViewModel"/> with required dependencies.
    /// </summary>
    /// <param name="serverConnectionRepository">Repository for server connection CRUD operations.</param>
    /// <param name="credentialStore">Secure storage for server credentials.</param>
    /// <param name="connectionManager">Manager for server connectivity and reachability checks.</param>
    /// <param name="httpClientFactory">Factory used to create a short-lived HTTP client for direct health-check probes against the URL in the form.</param>
    /// <param name="navigationService">Service for Shell navigation.</param>
    /// <param name="popupService">Service for popup/dialog operations.</param>
    /// <param name="providerService">Service for provider and model operations.</param>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is <see langword="null"/>.</exception>
    public ServerDetailViewModel(
        IServerConnectionRepository serverConnectionRepository,
        IServerCredentialStore credentialStore,
        IOpencodeConnectionManager connectionManager,
        IHttpClientFactory httpClientFactory,
        INavigationService navigationService,
        IAppPopupService popupService,
        IProviderService providerService)
    {
        ArgumentNullException.ThrowIfNull(serverConnectionRepository);
        ArgumentNullException.ThrowIfNull(credentialStore);
        ArgumentNullException.ThrowIfNull(connectionManager);
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(navigationService);
        ArgumentNullException.ThrowIfNull(popupService);
        ArgumentNullException.ThrowIfNull(providerService);

        _serverConnectionRepository = serverConnectionRepository;
        _credentialStore = credentialStore;
        _connectionManager = connectionManager;
        _httpClientFactory = httpClientFactory;
        _navigationService = navigationService;
        _popupService = popupService;
        _providerService = providerService;
    }

    // ─── Observable state ─────────────────────────────────────────────────────

    /// <summary>Gets or sets the display name for the server connection.</summary>
    [ObservableProperty]
    private string _name = string.Empty;

    /// <summary>
    /// Gets or sets the URL of the server (e.g. <c>http://192.168.1.10:4096</c>).
    /// Parsed on Save to extract <c>Host</c>, <c>Port</c>, and <c>UseHttps</c>.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(TestConnectionCommand))]
    private string _url = string.Empty;

    /// <summary>
    /// Gets or sets the password entered by the user.
    /// Never pre-filled from secure storage — only written on Save.
    /// </summary>
    [ObservableProperty]
    private string _password = string.Empty;

    /// <summary>
    /// Gets or sets the placeholder text for the password field.
    /// Changes to "Password saved — leave empty to keep unchanged" in Edit mode when a password exists.
    /// </summary>
    [ObservableProperty]
    private string _passwordPlaceholder = "Leave empty if not required";

    /// <summary>Gets or sets whether the ViewModel is in Edit mode (existing server) vs. Add mode.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DeleteCommand))]
    private bool _isEditMode;

    /// <summary>
    /// Gets or sets whether the server has been successfully saved to the repository.
    /// Enables the <see cref="SetActiveCommand"/>.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SetActiveCommand))]
    private bool _isSaved;

    /// <summary>Gets or sets whether the form data is currently being loaded from the repository.</summary>
    [ObservableProperty]
    private bool _isLoading;

    /// <summary>Gets or sets whether a save operation is currently in progress.</summary>
    [ObservableProperty]
    private bool _isSaving;

    /// <summary>Gets or sets whether a connection test is currently in progress.</summary>
    [ObservableProperty]
    private bool _isTesting;

    /// <summary>Gets or sets whether an activation operation is currently in progress.</summary>
    [ObservableProperty]
    private bool _isActivating;

    /// <summary>Gets or sets whether a delete operation is currently in progress.</summary>
    [ObservableProperty]
    private bool _isDeleting;

    /// <summary>Gets or sets the inline validation error message, or null when the form is valid.</summary>
    [ObservableProperty]
    private string? _validationError;

    /// <summary>Gets or sets whether a connection test has been performed since the last URL change.</summary>
    [ObservableProperty]
    private bool _isConnectionTested;

    /// <summary>Gets or sets whether the last connection test was successful.</summary>
    [ObservableProperty]
    private bool _isConnectionSuccessful;

    /// <summary>Gets or sets the status message from the last connection test.</summary>
    [ObservableProperty]
    private string _connectionStatusMessage = string.Empty;

    /// <summary>Gets or sets the inline status message after a Set as Active operation, or null.</summary>
    [ObservableProperty]
    private string? _activationStatusMessage;

    /// <summary>Gets or sets the display name of the current default model, or "No model selected" when null.</summary>
    [ObservableProperty]
    private string _defaultModelName = "No model selected";

    /// <summary>The raw default model ID stored on the server connection, or null.</summary>
    private string? _defaultModelId;

    // ─── Initialisation ───────────────────────────────────────────────────────

    /// <summary>
    /// Initialises the ViewModel for Add mode, Edit mode, or discovered-server pre-population.
    /// </summary>
    /// <param name="serverId">The ID of an existing server to edit, or null for Add mode.</param>
    /// <param name="discoveredHost">Pre-populated host from mDNS discovery (Add mode only).</param>
    /// <param name="discoveredPort">Pre-populated port from mDNS discovery (Add mode only).</param>
    /// <param name="discoveredName">Pre-populated name from mDNS discovery (Add mode only).</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task InitialiseAsync(
        string? serverId,
        string? discoveredHost = null,
        int discoveredPort = 0,
        string? discoveredName = null,
        CancellationToken ct = default)
    {
        if (!string.IsNullOrEmpty(serverId))
        {
            // Edit mode — load existing server from repository.
            IsLoading = true;

            try
            {
                var dto = await _serverConnectionRepository.GetByIdAsync(serverId, ct);

                if (dto is not null)
                {
                    IsEditMode = true;
                    IsSaved = true;
                    _savedServerId = dto.Id;
                    _originalHasPassword = dto.HasPassword;

                    Name = dto.Name;
                    Url = ServerUrlHelper.BuildUrl(dto.Host, dto.Port, dto.UseHttps);
                    PasswordPlaceholder = dto.HasPassword
                        ? "Password saved — leave empty to keep unchanged"
                        : "Leave empty if not required";

                    // Load default model display name (REQ-011)
                    _defaultModelId = dto.DefaultModelId;
                    DefaultModelName = dto.DefaultModelId ?? "No model selected";
                }
            }
            catch (Exception ex)
            {
                SentryHelper.CaptureException(ex, new Dictionary<string, object>
                {
                    ["context"] = "ServerDetailViewModel.InitialiseAsync",
                    ["serverId"] = serverId,
                });
            }
            finally
            {
                IsLoading = false;
            }
        }
        else if (!string.IsNullOrEmpty(discoveredHost))
        {
            // Add mode — pre-populate from mDNS discovery result.
            IsEditMode = false;
            IsSaved = false;
            Name = discoveredName ?? discoveredHost;
            Url = $"http://{discoveredHost}:{discoveredPort}";
        }
        else
        {
            // Add mode — blank form.
            IsEditMode = false;
            IsSaved = false;
        }
    }

    // ─── Commands ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Validates the form and persists the server connection.
    /// In Add mode: calls <see cref="IServerConnectionRepository.AddAsync"/> then optionally
    /// <see cref="IServerCredentialStore.SavePasswordAsync"/>.
    /// In Edit mode: calls <see cref="IServerConnectionRepository.UpdateAsync"/> and updates
    /// or removes the credential based on the password field value.
    /// Pops back to <c>ServerManagementPage</c> on success.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    [RelayCommand]
    private async Task SaveAsync(CancellationToken ct)
    {
#if DEBUG
        var sw = Stopwatch.StartNew();
        DebugLogger.LogCommand(nameof(SaveAsync), "start");
        try
        {
#endif
        ValidationError = null;

        // Validate Name
        if (string.IsNullOrWhiteSpace(Name))
        {
            ValidationError = "Name is required.";
            return;
        }

        // Validate URL
        if (!ServerUrlHelper.TryParse(Url, out var host, out var port, out var useHttps))
        {
            ValidationError = "Invalid URL. Use http:// or https:// with a valid host.";
            return;
        }

        IsSaving = true;

        try
        {
            var dto = new ServerConnectionDto(
                Id: _savedServerId ?? string.Empty,
                Name: Name.Trim(),
                Host: host,
                Port: port,
                Username: !string.IsNullOrWhiteSpace(Password) ? "opencode" : null,
                IsActive: false,
                DiscoveredViaMdns: false,
                UseHttps: useHttps,
                CreatedAt: DateTime.UtcNow,
                UpdatedAt: DateTime.UtcNow,
                HasPassword: !string.IsNullOrWhiteSpace(Password),
                DefaultModelId: _defaultModelId);

            if (!IsEditMode)
            {
                // Add mode — create new record.
                var saved = await _serverConnectionRepository.AddAsync(dto, ct);
                _savedServerId = saved.Id;

                if (!string.IsNullOrWhiteSpace(Password))
                {
                    await _credentialStore.SavePasswordAsync(saved.Id, Password.Trim(), ct);
                }

                IsSaved = true;
                IsEditMode = true;
                _originalHasPassword = !string.IsNullOrWhiteSpace(Password);
            }
            else
            {
                // Edit mode — update existing record.
                await _serverConnectionRepository.UpdateAsync(dto with { Id = _savedServerId! }, ct);

                if (!string.IsNullOrWhiteSpace(Password))
                {
                    // New password provided — overwrite stored credential.
                    await _credentialStore.SavePasswordAsync(_savedServerId!, Password.Trim(), ct);
                    _originalHasPassword = true;
                }
                else if (_originalHasPassword)
                {
                    // Password field cleared and a credential was previously stored — delete it.
                    await _credentialStore.DeletePasswordAsync(_savedServerId!, ct);
                    _originalHasPassword = false;
                }
                // No-op: field empty and no password was ever stored.
            }

            await _navigationService.PopAsync(ct);
        }
        catch (Exception ex)
        {
            ValidationError = $"Save failed: {ex.Message}";
            SentryHelper.CaptureException(ex, new Dictionary<string, object>
            {
                ["context"] = "ServerDetailViewModel.SaveCommand",
            });
        }
        finally
        {
            IsSaving = false;
        }
#if DEBUG
        sw.Stop();
        DebugLogger.LogCommand(nameof(SaveAsync), "complete", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            DebugLogger.LogCommand(nameof(SaveAsync), "failed", error: $"{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            throw;
        }
#endif
    }

    /// <summary>
    /// Tests the connection to the URL entered in the form by calling <c>GET /global/health</c>
    /// directly on that host and port. Uses a 10-second timeout. Does not persist any data
    /// and does not affect the active server.
    /// </summary>
    /// <remarks>
    /// Uses <see cref="IHttpClientFactory"/> to create a short-lived HTTP client that probes
    /// the exact URL in the form — not the currently active server. This ensures the test
    /// reflects the server the user intends to add or edit.
    /// Enabled only when <see cref="Url"/> is non-empty.
    /// </remarks>
    /// <param name="ct">Cancellation token.</param>
    [RelayCommand(CanExecute = nameof(CanTestConnection))]
    private async Task TestConnectionAsync(CancellationToken ct)
    {
#if DEBUG
        var sw = Stopwatch.StartNew();
        DebugLogger.LogCommand(nameof(TestConnectionAsync), "start");
        try
        {
#endif
        if (!ServerUrlHelper.TryParse(Url, out var host, out var port, out var useHttps))
        {
            IsConnectionTested = true;
            IsConnectionSuccessful = false;
            ConnectionStatusMessage = "Invalid URL format. Use http:// or https://";
            return;
        }

        IsTesting = true;
        IsConnectionTested = false;

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(10));

            // Build the health endpoint URL directly from the form's parsed components.
            // This probes the server the user typed — not the currently active server.
            var scheme = useHttps ? "https" : "http";
            var defaultPort = useHttps ? 443 : 80;
            var baseUrl = port == defaultPort
                ? $"{scheme}://{host}"
                : $"{scheme}://{host}:{port}";
            var healthUrl = $"{baseUrl}/global/health";

            // Use "discovery-probe" (5-second timeout, no resilience pipeline) — not "opencode".
            // The form probes an arbitrary URL the user typed; failed probes must never
            // trigger the circuit breaker that protects real API calls on the "opencode" client.
            var client = _httpClientFactory.CreateClient("discovery-probe");
            using var response = await client.GetAsync(healthUrl, timeoutCts.Token);

            if (!response.IsSuccessStatusCode)
            {
                IsConnectionSuccessful = false;
                ConnectionStatusMessage = $"Connection failed: HTTP {(int)response.StatusCode}";
                return;
            }

            var body = await response.Content.ReadAsStringAsync(timeoutCts.Token);
            using var doc = JsonDocument.Parse(body);

            var healthy = doc.RootElement.TryGetProperty("healthy", out var healthyProp)
                && healthyProp.ValueKind == JsonValueKind.True;

            if (healthy)
            {
                var version = doc.RootElement.TryGetProperty("version", out var versionProp)
                    ? versionProp.GetString() ?? "unknown"
                    : "unknown";
                IsConnectionSuccessful = true;
                ConnectionStatusMessage = $"Connected — server v{version}";
            }
            else
            {
                IsConnectionSuccessful = false;
                ConnectionStatusMessage = "Connection failed: server returned unhealthy status.";
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            IsConnectionSuccessful = false;
            ConnectionStatusMessage = "Connection timed out. Check the URL and try again.";
        }
        catch (HttpRequestException ex)
        {
            IsConnectionSuccessful = false;
            ConnectionStatusMessage = $"Connection failed: {ex.Message}";
        }
        catch (Exception ex)
        {
            IsConnectionSuccessful = false;
            ConnectionStatusMessage = $"Unexpected error: {ex.Message}";
            SentryHelper.CaptureException(ex, new Dictionary<string, object>
            {
                ["context"] = "ServerDetailViewModel.TestConnectionAsync",
                ["url"] = Url,
            });
        }
        finally
        {
            IsTesting = false;
            IsConnectionTested = true;
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
    /// Sets the current server as the active connection and triggers a reachability check.
    /// Pops back to <c>ServerManagementPage</c> after activation.
    /// </summary>
    /// <remarks>
    /// Enabled only after a successful <see cref="SaveCommand"/> (i.e. <see cref="IsSaved"/> is <c>true</c>).
    /// </remarks>
    /// <param name="ct">Cancellation token.</param>
    [RelayCommand(CanExecute = nameof(IsSaved))]
    private async Task SetActiveAsync(CancellationToken ct)
    {
#if DEBUG
        var sw = Stopwatch.StartNew();
        DebugLogger.LogCommand(nameof(SetActiveAsync), "start");
        try
        {
#endif
        IsActivating = true;
        ActivationStatusMessage = null;

        try
        {
            await _serverConnectionRepository.SetActiveAsync(_savedServerId!, ct);

            var reachable = await _connectionManager.IsServerReachableAsync(ct);
            ActivationStatusMessage = reachable
                ? "Now active — server reachable"
                : "Set as active — server unreachable";

            await _navigationService.PopAsync(ct);
        }
        catch (Exception ex)
        {
            ActivationStatusMessage = $"Activation failed: {ex.Message}";
            SentryHelper.CaptureException(ex, new Dictionary<string, object>
            {
                ["context"] = "ServerDetailViewModel.SetActiveCommand",
            });
        }
        finally
        {
            IsActivating = false;
        }
#if DEBUG
        sw.Stop();
        DebugLogger.LogCommand(nameof(SetActiveAsync), "complete", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            DebugLogger.LogCommand(nameof(SetActiveAsync), "failed", error: $"{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            throw;
        }
#endif
    }

    /// <summary>
    /// Shows a confirmation dialog and, if confirmed, deletes the server connection.
    /// Pops back to <c>ServerManagementPage</c> after deletion.
    /// </summary>
    /// <remarks>
    /// Only available in Edit mode (<see cref="IsEditMode"/> is <c>true</c>).
    /// The repository's <c>DeleteAsync</c> also removes the associated credential from
    /// <see cref="IServerCredentialStore"/> per the repository contract.
    /// </remarks>
    /// <param name="ct">Cancellation token.</param>
    [RelayCommand(CanExecute = nameof(IsEditMode))]
    private async Task DeleteAsync(CancellationToken ct)
    {
#if DEBUG
        var sw = Stopwatch.StartNew();
        DebugLogger.LogCommand(nameof(DeleteAsync), "start");
        try
        {
#endif
        IsDeleting = true;

        try
        {
            var confirmed = await _popupService.ShowConfirmDeleteAsync(
                "Delete Server",
                "Are you sure you want to remove this server? This action cannot be undone.",
                ct);

            if (!confirmed)
                return;

            await _serverConnectionRepository.DeleteAsync(_savedServerId!, ct);
            await _navigationService.PopAsync(ct);
        }
        catch (Exception ex)
        {
            ValidationError = $"Delete failed: {ex.Message}";
            SentryHelper.CaptureException(ex, new Dictionary<string, object>
            {
                ["context"] = "ServerDetailViewModel.DeleteCommand",
            });
        }
        finally
        {
            IsDeleting = false;
        }
#if DEBUG
        sw.Stop();
        DebugLogger.LogCommand(nameof(DeleteAsync), "complete", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            DebugLogger.LogCommand(nameof(DeleteAsync), "failed", error: $"{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            throw;
        }
#endif
    }

    /// <summary>
    /// Opens the model picker popup to change the default model for this server (REQ-011).
    /// On selection, persists the new default model ID via the repository.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    [RelayCommand]
    private async Task ChangeDefaultModelAsync(CancellationToken ct)
    {
#if DEBUG
        var sw = Stopwatch.StartNew();
        DebugLogger.LogCommand(nameof(ChangeDefaultModelAsync), "start");
        try
        {
#endif
        if (string.IsNullOrEmpty(_savedServerId))
            return;

        await _popupService.ShowModelPickerAsync(
            onModelSelected: (modelId) =>
            {
                // Fire-and-forget with error handling — Action<string> cannot be async.
                _ = SafeSetDefaultModelAsync(modelId);
            },
            ct);
#if DEBUG
        sw.Stop();
        DebugLogger.LogCommand(nameof(ChangeDefaultModelAsync), "complete", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            DebugLogger.LogCommand(nameof(ChangeDefaultModelAsync), "failed", error: $"{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            throw;
        }
#endif
    }

    // ─── Private helpers ─────────────────────────────────────────────────────

    /// <summary>
    /// Safely persists the selected default model and updates the UI.
    /// Called as fire-and-forget from the <see cref="ShowModelPickerAsync"/> callback
    /// (which accepts <see cref="Action{String}"/> and cannot be async).
    /// All exceptions are captured to Sentry to prevent unobserved task exceptions.
    /// </summary>
    /// <param name="modelId">The selected model ID in "providerId/modelId" format.</param>
    private async Task SafeSetDefaultModelAsync(string modelId)
    {
        try
        {
            if (string.IsNullOrEmpty(_savedServerId))
                return;

            var success = await _serverConnectionRepository.SetDefaultModelAsync(_savedServerId, modelId);
            if (success)
            {
                _defaultModelId = modelId;
                DefaultModelName = modelId;
            }
        }
        catch (Exception ex)
        {
            SentryHelper.CaptureException(ex, new Dictionary<string, object>
            {
                ["context"] = "ServerDetailViewModel.SafeSetDefaultModelAsync",
            });
        }
    }

    // ─── CanExecute helpers ───────────────────────────────────────────────────

    /// <summary>Returns <c>true</c> when the URL field is non-empty, enabling the Test Connection button.</summary>
    private bool CanTestConnection() => !string.IsNullOrWhiteSpace(Url);
}
