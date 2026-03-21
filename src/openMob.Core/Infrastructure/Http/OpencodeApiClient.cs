using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using openMob.Core.Infrastructure.Http.Dtos.Opencode;
using openMob.Core.Infrastructure.Http.Dtos.Opencode.Requests;
using openMob.Core.Infrastructure.Logging;
using openMob.Core.Infrastructure.Settings;
using openMob.Core.Services;

namespace openMob.Core.Infrastructure.Http;

/// <summary>
/// Default implementation of <see cref="IOpencodeApiClient"/>.
/// Uses <see cref="IHttpClientFactory"/> with the named client <c>"opencode"</c>.
/// Base URL and auth headers are resolved at runtime from <see cref="IOpencodeConnectionManager"/>.
/// </summary>
/// <remarks>
/// <para>
/// Retry policy: on <see cref="ErrorKind.NetworkUnreachable"/> or <see cref="ErrorKind.ServerError"/>,
/// the request is retried with exponential backoff. The default delays are 2s then 4s
/// (giving 3 total attempts). During retries, <see cref="IOpencodeConnectionManager.ConnectionStatus"/>
/// is set to <see cref="ServerConnectionStatus.Connecting"/>. After all retries are exhausted,
/// it is set to <see cref="ServerConnectionStatus.Error"/>.
/// </para>
/// <para>
/// <see cref="IsWaitingForServer"/> is set to <c>true</c> for the entire duration of any
/// in-flight request and reset to <c>false</c> in the finally block.
/// </para>
/// </remarks>
internal sealed class OpencodeApiClient : IOpencodeApiClient
{
    /// <summary>Default inter-attempt delays: 2s before attempt 2, 4s before attempt 3.</summary>
    private static readonly TimeSpan[] DefaultRetryDelays = [TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4)];

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOpencodeConnectionManager _connectionManager;
    private readonly IOpencodeSettingsService _settingsService;
    private readonly Lazy<IActiveProjectService> _activeProjectService;
    private readonly TimeSpan[] _retryDelays;
    private volatile bool _isWaitingForServer;

    /// <summary>
    /// Initialises the client with the required dependencies.
    /// </summary>
    /// <param name="httpClientFactory">Factory for creating named HTTP clients.</param>
    /// <param name="connectionManager">Resolves the active server base URL and auth header.</param>
    /// <param name="settingsService">Provides the configured request timeout.</param>
    /// <param name="activeProjectService">
    /// Lazy wrapper around <see cref="IActiveProjectService"/>. Deferred resolution breaks the
    /// circular dependency chain: <c>OpencodeApiClient → IActiveProjectService → IProjectService → IOpencodeApiClient</c>.
    /// The service is only resolved on first HTTP request, not at construction time.
    /// </param>
    /// <param name="retryDelays">
    /// Optional override for inter-attempt retry delays. Defaults to [2s, 4s] (3 total attempts).
    /// Pass shorter delays in tests to avoid real-time waits.
    /// </param>
    public OpencodeApiClient(
        IHttpClientFactory httpClientFactory,
        IOpencodeConnectionManager connectionManager,
        IOpencodeSettingsService settingsService,
        Lazy<IActiveProjectService> activeProjectService,
        TimeSpan[]? retryDelays = null)
    {
        _httpClientFactory = httpClientFactory;
        _connectionManager = connectionManager;
        _settingsService = settingsService;
        _activeProjectService = activeProjectService;
        _retryDelays = retryDelays ?? DefaultRetryDelays;
    }

    /// <inheritdoc />
    public bool IsWaitingForServer => _isWaitingForServer;

    /// <inheritdoc />
    public event Action<bool>? IsWaitingForServerChanged;

    // ─── Private helpers ──────────────────────────────────────────────────────

    private void SetWaiting(bool value)
    {
        _isWaitingForServer = value;
        IsWaitingForServerChanged?.Invoke(value);
    }

    /// <summary>
    /// Core execution helper. Resolves the base URL, injects auth, applies timeout,
    /// executes the request via <paramref name="requestFactory"/>, and handles
    /// retry logic for transient failures.
    /// </summary>
    /// <remarks>
    /// The <paramref name="requestFactory"/> receives the linked <see cref="CancellationToken"/>
    /// (combining the caller's token with the per-request timeout) so that the
    /// <see cref="HttpClient"/> request is properly cancelled when the timeout fires.
    /// </remarks>
    private async Task<OpencodeResult<T>> ExecuteAsync<T>(
        Func<HttpClient, string, CancellationToken, Task<HttpResponseMessage>> requestFactory,
        CancellationToken ct)
    {
        var baseUrl = await _connectionManager.GetBaseUrlAsync(ct).ConfigureAwait(false);
        if (baseUrl is null)
        {
            return OpencodeResult<T>.Failure(new OpencodeApiError(
                ErrorKind.NoActiveServer,
                "No active server connection is configured.",
                null,
                null));
        }

        SetWaiting(true);

        try
        {
            var authHeader = await _connectionManager.GetBasicAuthHeaderAsync(ct).ConfigureAwait(false);
            var timeoutSeconds = _settingsService.GetTimeoutSeconds();

            // Total attempts = number of retry delays + 1
            var maxAttempts = _retryDelays.Length + 1;
            OpencodeApiError? lastError = null;

            for (var attempt = 0; attempt < maxAttempts; attempt++)
            {
                if (attempt > 0)
                {
                    // Signal retry state and wait before next attempt
                    _connectionManager.SetConnectionStatus(ServerConnectionStatus.Connecting);
                    await Task.Delay(_retryDelays[attempt - 1], ct).ConfigureAwait(false);
                }

                try
                {
                    var client = _httpClientFactory.CreateClient("opencode");

                    if (authHeader is not null)
                    {
                        // Strip the "Basic " prefix — AuthenticationHeaderValue adds it back
                        var encoded = authHeader.StartsWith("Basic ", StringComparison.Ordinal)
                            ? authHeader["Basic ".Length..]
                            : authHeader;
                        client.DefaultRequestHeaders.Authorization =
                            new AuthenticationHeaderValue("Basic", encoded);
                    }

                    // Inject the active project directory so the server uses the correct project context.
                    // The server reads this from the x-opencode-directory header on every request.
                    var activeProject = await _activeProjectService.Value.GetActiveProjectAsync(ct).ConfigureAwait(false);
                    if (activeProject?.Worktree is { Length: > 0 } worktree)
                        client.DefaultRequestHeaders.TryAddWithoutValidation("x-opencode-directory", worktree);

                    // Apply per-request timeout via a linked CancellationTokenSource.
                    // Pass cts.Token into requestFactory so the HttpClient call is cancelled
                    // when the timeout fires, not just a WaitAsync wrapper.
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

#if DEBUG
                    var sw = Stopwatch.StartNew();
#endif
                    using var response = await requestFactory(client, baseUrl, cts.Token)
                        .ConfigureAwait(false);
#if DEBUG
                    sw.Stop();
                    string? reqBody = null;
                    string? resBody = null;
                    try
                    {
                        if (response.RequestMessage?.Content is not null)
                            reqBody = await response.RequestMessage.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                    }
                    catch { /* best-effort — do not fail the request */ }
#endif

                    // ── Map HTTP status codes ──────────────────────────────────
                    if (response.IsSuccessStatusCode)
                    {
                        if (response.StatusCode == HttpStatusCode.NoContent)
                        {
#if DEBUG
                            DebugLogger.LogHttp(
                                response.RequestMessage?.Method.Method ?? "?",
                                response.RequestMessage?.RequestUri?.ToString() ?? "",
                                reqBody,
                                (int)response.StatusCode,
                                null,
                                sw.ElapsedMilliseconds);
#endif
                            // 204 No Content — for bool methods return true; for others return default
                            if (typeof(T) == typeof(bool))
                                return OpencodeResult<T>.Success((T)(object)true);

                            return OpencodeResult<T>.Success(default(T)!);
                        }

                        var value = await response.Content
                            .ReadFromJsonAsync<T>(ct)
                            .ConfigureAwait(false);

#if DEBUG
                        try { resBody = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false); } catch { }
                        DebugLogger.LogHttp(
                            response.RequestMessage?.Method.Method ?? "?",
                            response.RequestMessage?.RequestUri?.ToString() ?? "",
                            reqBody,
                            (int)response.StatusCode,
                            resBody,
                            sw.ElapsedMilliseconds);
#endif
                        return OpencodeResult<T>.Success(value!);
                    }

                    var statusCode = (int)response.StatusCode;

                    if (statusCode == 401)
                    {
#if DEBUG
                        DebugLogger.LogHttp(
                            response.RequestMessage?.Method.Method ?? "?",
                            response.RequestMessage?.RequestUri?.ToString() ?? "",
                            reqBody,
                            statusCode,
                            null,
                            sw.ElapsedMilliseconds);
#endif
                        return OpencodeResult<T>.Failure(new OpencodeApiError(
                            ErrorKind.Unauthorized,
                            "Authentication failed. Check your server credentials.",
                            statusCode,
                            null));
                    }

                    if (statusCode == 404)
                    {
#if DEBUG
                        DebugLogger.LogHttp(
                            response.RequestMessage?.Method.Method ?? "?",
                            response.RequestMessage?.RequestUri?.ToString() ?? "",
                            reqBody,
                            statusCode,
                            null,
                            sw.ElapsedMilliseconds);
#endif
                        return OpencodeResult<T>.Failure(new OpencodeApiError(
                            ErrorKind.NotFound,
                            "The requested resource was not found on the server.",
                            statusCode,
                            null));
                    }

                    if (statusCode >= 400 && statusCode < 500)
                    {
                        // Other 4xx — no retry
                        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
#if DEBUG
                        DebugLogger.LogHttp(
                            response.RequestMessage?.Method.Method ?? "?",
                            response.RequestMessage?.RequestUri?.ToString() ?? "",
                            reqBody,
                            statusCode,
                            body,
                            sw.ElapsedMilliseconds);
#endif
                        return OpencodeResult<T>.Failure(new OpencodeApiError(
                            ErrorKind.Unknown,
                            $"Server returned {statusCode}: {body}",
                            statusCode,
                            null));
                    }

                    if (statusCode >= 500)
                    {
                        // 5xx — eligible for retry
                        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
#if DEBUG
                        DebugLogger.LogHttp(
                            response.RequestMessage?.Method.Method ?? "?",
                            response.RequestMessage?.RequestUri?.ToString() ?? "",
                            reqBody,
                            statusCode,
                            body,
                            sw.ElapsedMilliseconds);
#endif
                        lastError = new OpencodeApiError(
                            ErrorKind.ServerError,
                            $"Server error {statusCode}: {body}",
                            statusCode,
                            null);
                        // Fall through to next attempt
                    }
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    // User explicitly cancelled — rethrow immediately, no retry
                    throw;
                }
                catch (OperationCanceledException ex)
                {
                    // Timeout (our linked CTS fired, not the caller's token)
                    return OpencodeResult<T>.Failure(new OpencodeApiError(
                        ErrorKind.Timeout,
                        $"The request timed out after {timeoutSeconds} seconds.",
                        null,
                        ex));
                }
                catch (HttpRequestException ex)
                {
                    // Network-level failure — eligible for retry
                    lastError = new OpencodeApiError(
                        ErrorKind.NetworkUnreachable,
                        $"Could not reach the server: {ex.Message}",
                        null,
                        ex);
                    // Fall through to next attempt
                }
            }

            // All retries exhausted
            _connectionManager.SetConnectionStatus(ServerConnectionStatus.Error);

            return OpencodeResult<T>.Failure(lastError ?? new OpencodeApiError(
                ErrorKind.Unknown,
                "An unknown error occurred after all retry attempts.",
                null,
                null));
        }
        finally
        {
            SetWaiting(false);
        }
    }

    // ─── Global ───────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public Task<OpencodeResult<HealthDto>> GetHealthAsync(CancellationToken ct = default)
        => ExecuteAsync<HealthDto>(
            (client, baseUrl, token) => client.GetAsync($"{baseUrl}/global/health", token),
            ct);

    // ─── Project ──────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public Task<OpencodeResult<IReadOnlyList<ProjectDto>>> GetProjectsAsync(CancellationToken ct = default)
        => ExecuteAsync<IReadOnlyList<ProjectDto>>(
            (client, baseUrl, token) => client.GetAsync($"{baseUrl}/project", token),
            ct);

    /// <inheritdoc />
    public Task<OpencodeResult<ProjectDto>> GetCurrentProjectAsync(CancellationToken ct = default)
        => ExecuteAsync<ProjectDto>(
            (client, baseUrl, token) => client.GetAsync($"{baseUrl}/project/current", token),
            ct);

    // ─── Path & VCS ───────────────────────────────────────────────────────────

    /// <inheritdoc />
    public Task<OpencodeResult<PathDto>> GetPathAsync(CancellationToken ct = default)
        => ExecuteAsync<PathDto>(
            (client, baseUrl, token) => client.GetAsync($"{baseUrl}/path", token),
            ct);

    /// <inheritdoc />
    public Task<OpencodeResult<VcsInfoDto>> GetVcsInfoAsync(CancellationToken ct = default)
        => ExecuteAsync<VcsInfoDto>(
            (client, baseUrl, token) => client.GetAsync($"{baseUrl}/vcs", token),
            ct);

    // ─── Config ───────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public Task<OpencodeResult<ConfigDto>> GetConfigAsync(CancellationToken ct = default)
        => ExecuteAsync<ConfigDto>(
            (client, baseUrl, token) => client.GetAsync($"{baseUrl}/config", token),
            ct);

    /// <inheritdoc />
    public Task<OpencodeResult<ConfigDto>> UpdateConfigAsync(UpdateConfigRequest request, CancellationToken ct = default)
        => ExecuteAsync<ConfigDto>(
            (client, baseUrl, token) => client.PutAsJsonAsync($"{baseUrl}/config", request, token),
            ct);

    /// <inheritdoc />
    public Task<OpencodeResult<ConfigProvidersDto>> GetConfigProvidersAsync(CancellationToken ct = default)
        => ExecuteAsync<ConfigProvidersDto>(
            (client, baseUrl, token) => client.GetAsync($"{baseUrl}/config/providers", token),
            ct);

    // ─── Provider ─────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public Task<OpencodeResult<ProviderListResponseDto>> GetProvidersAsync(CancellationToken ct = default)
        => ExecuteAsync<ProviderListResponseDto>(
            (client, baseUrl, token) => client.GetAsync($"{baseUrl}/provider", token),
            ct);

    /// <inheritdoc />
    public Task<OpencodeResult<IReadOnlyList<ProviderAuthMethodDto>>> GetProviderAuthMethodsAsync(CancellationToken ct = default)
        => ExecuteAsync<IReadOnlyList<ProviderAuthMethodDto>>(
            (client, baseUrl, token) => client.GetAsync($"{baseUrl}/provider/auth", token),
            ct);

    /// <inheritdoc />
    public Task<OpencodeResult<ProviderAuthAuthorizationDto>> AuthorizeProviderOAuthAsync(string providerId, CancellationToken ct = default)
        => ExecuteAsync<ProviderAuthAuthorizationDto>(
            (client, baseUrl, token) => client.GetAsync($"{baseUrl}/provider/{Uri.EscapeDataString(providerId)}/auth/authorize", token),
            ct);

    /// <inheritdoc />
    public Task<OpencodeResult<bool>> HandleProviderOAuthCallbackAsync(string providerId, OAuthCallbackRequest request, CancellationToken ct = default)
        => ExecuteAsync<bool>(
            (client, baseUrl, token) => client.PostAsJsonAsync($"{baseUrl}/provider/{Uri.EscapeDataString(providerId)}/auth/callback", request, token),
            ct);

    // ─── Sessions ─────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public Task<OpencodeResult<IReadOnlyList<SessionDto>>> GetSessionsAsync(CancellationToken ct = default)
        => ExecuteAsync<IReadOnlyList<SessionDto>>(
            (client, baseUrl, token) => client.GetAsync($"{baseUrl}/session", token),
            ct);

    /// <inheritdoc />
    public Task<OpencodeResult<SessionDto>> GetSessionAsync(string id, CancellationToken ct = default)
        => ExecuteAsync<SessionDto>(
            (client, baseUrl, token) => client.GetAsync($"{baseUrl}/session/{Uri.EscapeDataString(id)}", token),
            ct);

    /// <inheritdoc />
    public Task<OpencodeResult<IReadOnlyDictionary<string, SessionStatusDto>>> GetSessionStatusAsync(CancellationToken ct = default)
        => ExecuteAsync<IReadOnlyDictionary<string, SessionStatusDto>>(
            (client, baseUrl, token) => client.GetAsync($"{baseUrl}/session/status", token),
            ct);

    /// <inheritdoc />
    public Task<OpencodeResult<IReadOnlyList<SessionDto>>> GetSessionChildrenAsync(string id, CancellationToken ct = default)
        => ExecuteAsync<IReadOnlyList<SessionDto>>(
            (client, baseUrl, token) => client.GetAsync($"{baseUrl}/session/{Uri.EscapeDataString(id)}/children", token),
            ct);

    /// <inheritdoc />
    public Task<OpencodeResult<IReadOnlyList<TodoDto>>> GetSessionTodoAsync(string id, CancellationToken ct = default)
        => ExecuteAsync<IReadOnlyList<TodoDto>>(
            (client, baseUrl, token) => client.GetAsync($"{baseUrl}/session/{Uri.EscapeDataString(id)}/todo", token),
            ct);

    /// <inheritdoc />
    /// <remarks>
    /// The opencode <c>POST /session</c> endpoint accepts no request body.
    /// The <paramref name="request"/> parameter is retained for API compatibility but its fields are not serialised.
    /// The active project directory is injected globally via the <c>x-opencode-directory</c> header
    /// in <see cref="ExecuteAsync{T}"/>, so no per-method directory parameter is needed.
    /// </remarks>
    public Task<OpencodeResult<SessionDto>> CreateSessionAsync(CreateSessionRequest request, CancellationToken ct = default)
        => ExecuteAsync<SessionDto>(
            (client, baseUrl, token) => client.PostAsync($"{baseUrl}/session", null, token),
            ct);

    /// <inheritdoc />
    public Task<OpencodeResult<SessionDto>> UpdateSessionAsync(string id, UpdateSessionRequest request, CancellationToken ct = default)
        => ExecuteAsync<SessionDto>(
            (client, baseUrl, token) => client.PutAsJsonAsync($"{baseUrl}/session/{Uri.EscapeDataString(id)}", request, token),
            ct);

    /// <inheritdoc />
    public Task<OpencodeResult<bool>> DeleteSessionAsync(string id, CancellationToken ct = default)
        => ExecuteAsync<bool>(
            (client, baseUrl, token) => client.DeleteAsync($"{baseUrl}/session/{Uri.EscapeDataString(id)}", token),
            ct);

    /// <inheritdoc />
    public Task<OpencodeResult<bool>> InitSessionAsync(string id, InitSessionRequest request, CancellationToken ct = default)
        => ExecuteAsync<bool>(
            (client, baseUrl, token) => client.PostAsJsonAsync($"{baseUrl}/session/{Uri.EscapeDataString(id)}/init", request, token),
            ct);

    /// <inheritdoc />
    public Task<OpencodeResult<SessionDto>> ForkSessionAsync(string id, ForkSessionRequest request, CancellationToken ct = default)
        => ExecuteAsync<SessionDto>(
            (client, baseUrl, token) => client.PostAsJsonAsync($"{baseUrl}/session/{Uri.EscapeDataString(id)}/fork", request, token),
            ct);

    /// <inheritdoc />
    public Task<OpencodeResult<bool>> AbortSessionAsync(string id, CancellationToken ct = default)
        => ExecuteAsync<bool>(
            (client, baseUrl, token) => client.PostAsJsonAsync($"{baseUrl}/session/{Uri.EscapeDataString(id)}/abort", (object?)null, token),
            ct);

    /// <inheritdoc />
    public Task<OpencodeResult<SessionDto>> ShareSessionAsync(string id, CancellationToken ct = default)
        => ExecuteAsync<SessionDto>(
            (client, baseUrl, token) => client.PostAsJsonAsync($"{baseUrl}/session/{Uri.EscapeDataString(id)}/share", (object?)null, token),
            ct);

    /// <inheritdoc />
    public Task<OpencodeResult<SessionDto>> UnshareSessionAsync(string id, CancellationToken ct = default)
        => ExecuteAsync<SessionDto>(
            (client, baseUrl, token) => client.DeleteAsync($"{baseUrl}/session/{Uri.EscapeDataString(id)}/share", token),
            ct);

    /// <inheritdoc />
    public Task<OpencodeResult<IReadOnlyList<FileDiffDto>>> GetSessionDiffAsync(string id, string? messageId = null, CancellationToken ct = default)
    {
        var url = messageId is not null
            ? $"/session/{Uri.EscapeDataString(id)}/diff?messageId={Uri.EscapeDataString(messageId)}"
            : $"/session/{Uri.EscapeDataString(id)}/diff";

        return ExecuteAsync<IReadOnlyList<FileDiffDto>>(
            (client, baseUrl, token) => client.GetAsync($"{baseUrl}{url}", token),
            ct);
    }

    /// <inheritdoc />
    public Task<OpencodeResult<bool>> SummarizeSessionAsync(string id, SummarizeSessionRequest request, CancellationToken ct = default)
        => ExecuteAsync<bool>(
            (client, baseUrl, token) => client.PostAsJsonAsync($"{baseUrl}/session/{Uri.EscapeDataString(id)}/summarize", request, token),
            ct);

    /// <inheritdoc />
    public Task<OpencodeResult<bool>> RevertSessionAsync(string id, RevertSessionRequest request, CancellationToken ct = default)
        => ExecuteAsync<bool>(
            (client, baseUrl, token) => client.PostAsJsonAsync($"{baseUrl}/session/{Uri.EscapeDataString(id)}/revert", request, token),
            ct);

    /// <inheritdoc />
    public Task<OpencodeResult<bool>> UnrevertSessionAsync(string id, CancellationToken ct = default)
        => ExecuteAsync<bool>(
            (client, baseUrl, token) => client.PostAsJsonAsync($"{baseUrl}/session/{Uri.EscapeDataString(id)}/unrevert", (object?)null, token),
            ct);

    /// <inheritdoc />
    public Task<OpencodeResult<bool>> RespondToPermissionAsync(string id, string permissionId, PermissionResponseRequest request, CancellationToken ct = default)
        => ExecuteAsync<bool>(
            (client, baseUrl, token) => client.PostAsJsonAsync(
                $"{baseUrl}/session/{Uri.EscapeDataString(id)}/permission/{Uri.EscapeDataString(permissionId)}",
                request,
                token),
            ct);

    // ─── Messages ─────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public Task<OpencodeResult<IReadOnlyList<MessageWithPartsDto>>> GetMessagesAsync(string sessionId, int? limit = null, CancellationToken ct = default)
    {
        var url = limit.HasValue
            ? $"/session/{Uri.EscapeDataString(sessionId)}/message?limit={limit.Value}"
            : $"/session/{Uri.EscapeDataString(sessionId)}/message";

        return ExecuteAsync<IReadOnlyList<MessageWithPartsDto>>(
            (client, baseUrl, token) => client.GetAsync($"{baseUrl}{url}", token),
            ct);
    }

    /// <inheritdoc />
    public Task<OpencodeResult<MessageWithPartsDto>> GetMessageAsync(string sessionId, string messageId, CancellationToken ct = default)
        => ExecuteAsync<MessageWithPartsDto>(
            (client, baseUrl, token) => client.GetAsync(
                $"{baseUrl}/session/{Uri.EscapeDataString(sessionId)}/message/{Uri.EscapeDataString(messageId)}",
                token),
            ct);

    /// <inheritdoc />
    public Task<OpencodeResult<MessageWithPartsDto>> SendPromptAsync(string sessionId, SendPromptRequest request, CancellationToken ct = default)
        => ExecuteAsync<MessageWithPartsDto>(
            (client, baseUrl, token) => client.PostAsJsonAsync(
                $"{baseUrl}/session/{Uri.EscapeDataString(sessionId)}/message",
                request,
                token),
            ct);

    /// <inheritdoc />
    public Task<OpencodeResult<bool>> SendPromptAsyncNoWait(string sessionId, SendPromptRequest request, CancellationToken ct = default)
        => ExecuteAsync<bool>(
            (client, baseUrl, token) => client.PostAsJsonAsync(
                $"{baseUrl}/session/{Uri.EscapeDataString(sessionId)}/prompt_async",
                request,
                token),
            ct);

    /// <inheritdoc />
    public Task<OpencodeResult<MessageWithPartsDto>> SendCommandAsync(string sessionId, SendCommandRequest request, CancellationToken ct = default)
        => ExecuteAsync<MessageWithPartsDto>(
            (client, baseUrl, token) => client.PostAsJsonAsync(
                $"{baseUrl}/session/{Uri.EscapeDataString(sessionId)}/command",
                request,
                token),
            ct);

    /// <inheritdoc />
    public Task<OpencodeResult<MessageWithPartsDto>> RunShellAsync(string sessionId, RunShellRequest request, CancellationToken ct = default)
        => ExecuteAsync<MessageWithPartsDto>(
            (client, baseUrl, token) => client.PostAsJsonAsync(
                $"{baseUrl}/session/{Uri.EscapeDataString(sessionId)}/shell",
                request,
                token),
            ct);

    // ─── Commands ─────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public Task<OpencodeResult<IReadOnlyList<CommandDto>>> GetCommandsAsync(CancellationToken ct = default)
        => ExecuteAsync<IReadOnlyList<CommandDto>>(
            (client, baseUrl, token) => client.GetAsync($"{baseUrl}/command", token),
            ct);

    // ─── Files ────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public Task<OpencodeResult<IReadOnlyList<TextMatchDto>>> FindTextAsync(string pattern, CancellationToken ct = default)
        => ExecuteAsync<IReadOnlyList<TextMatchDto>>(
            (client, baseUrl, token) => client.GetAsync($"{baseUrl}/file/text?pattern={Uri.EscapeDataString(pattern)}", token),
            ct);

    /// <inheritdoc />
    public Task<OpencodeResult<IReadOnlyList<string>>> FindFilesAsync(FindFilesRequest request, CancellationToken ct = default)
    {
        var sb = new StringBuilder("/file");
        var hasQuery = false;

        if (request.Pattern is not null)
        {
            sb.Append($"?pattern={Uri.EscapeDataString(request.Pattern)}");
            hasQuery = true;
        }

        if (request.Path is not null)
        {
            sb.Append(hasQuery ? '&' : '?');
            sb.Append($"path={Uri.EscapeDataString(request.Path)}");
        }

        var relativeUrl = sb.ToString();

        return ExecuteAsync<IReadOnlyList<string>>(
            (client, baseUrl, token) => client.GetAsync($"{baseUrl}{relativeUrl}", token),
            ct);
    }

    /// <inheritdoc />
    public Task<OpencodeResult<IReadOnlyList<SymbolDto>>> FindSymbolsAsync(string query, CancellationToken ct = default)
        => ExecuteAsync<IReadOnlyList<SymbolDto>>(
            (client, baseUrl, token) => client.GetAsync($"{baseUrl}/file/symbol?query={Uri.EscapeDataString(query)}", token),
            ct);

    /// <inheritdoc />
    public Task<OpencodeResult<IReadOnlyList<FileNodeDto>>> GetFileTreeAsync(string? path = null, CancellationToken ct = default)
    {
        var url = path is not null
            ? $"/file/tree?path={Uri.EscapeDataString(path)}"
            : "/file/tree";

        return ExecuteAsync<IReadOnlyList<FileNodeDto>>(
            (client, baseUrl, token) => client.GetAsync($"{baseUrl}{url}", token),
            ct);
    }

    /// <inheritdoc />
    public Task<OpencodeResult<FileContentDto>> ReadFileAsync(string path, CancellationToken ct = default)
        => ExecuteAsync<FileContentDto>(
            (client, baseUrl, token) => client.GetAsync($"{baseUrl}/file/read?path={Uri.EscapeDataString(path)}", token),
            ct);

    /// <inheritdoc />
    public Task<OpencodeResult<IReadOnlyList<FileStatusDto>>> GetFileStatusAsync(CancellationToken ct = default)
        => ExecuteAsync<IReadOnlyList<FileStatusDto>>(
            (client, baseUrl, token) => client.GetAsync($"{baseUrl}/file/status", token),
            ct);

    // ─── Agents ───────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public Task<OpencodeResult<IReadOnlyList<AgentDto>>> GetAgentsAsync(CancellationToken ct = default)
        => ExecuteAsync<IReadOnlyList<AgentDto>>(
            (client, baseUrl, token) => client.GetAsync($"{baseUrl}/agent", token),
            ct);

    // ─── Auth ─────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public Task<OpencodeResult<bool>> SetProviderAuthAsync(string providerId, SetProviderAuthRequest request, CancellationToken ct = default)
        => ExecuteAsync<bool>(
            (client, baseUrl, token) => client.PostAsJsonAsync(
                $"{baseUrl}/provider/{Uri.EscapeDataString(providerId)}/auth",
                request,
                token),
            ct);

    // ─── Logging ──────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public Task<OpencodeResult<bool>> WriteLogAsync(WriteLogRequest request, CancellationToken ct = default)
        => ExecuteAsync<bool>(
            (client, baseUrl, token) => client.PostAsJsonAsync($"{baseUrl}/log", request, token),
            ct);

    // ─── Events (SSE) ─────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async IAsyncEnumerable<OpencodeEventDto> SubscribeToEventsAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var baseUrl = await _connectionManager.GetBaseUrlAsync(cancellationToken).ConfigureAwait(false);
        if (baseUrl is null)
            yield break;

        // Use "opencode-sse" (infinite timeout, no resilience pipeline) for SSE connections.
        // The "opencode" client has a 30-second timeout that would kill the long-lived stream.
        var client = _httpClientFactory.CreateClient("opencode-sse");

        var authHeader = await _connectionManager.GetBasicAuthHeaderAsync(cancellationToken).ConfigureAwait(false);
        if (authHeader is not null)
        {
            var encoded = authHeader.StartsWith("Basic ", StringComparison.Ordinal)
                ? authHeader["Basic ".Length..]
                : authHeader;
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", encoded);
        }

        // Inject the active project directory for SSE events too.
        var activeProject = await _activeProjectService.Value.GetActiveProjectAsync(cancellationToken).ConfigureAwait(false);
        if (activeProject?.Worktree is { Length: > 0 } worktree)
            client.DefaultRequestHeaders.TryAddWithoutValidation("x-opencode-directory", worktree);

        // Wrap the initial GET in a try/catch for network-level failures only.
        // HttpRequestException covers DNS failures, connection refused, etc.
        // All other non-cancellation exceptions are allowed to propagate (REQ-007).
        HttpResponseMessage response;
        try
        {
            response = await client.GetAsync(
                $"{baseUrl}/global/event",
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException)
        {
            yield break;
        }

        if (!response.IsSuccessStatusCode)
        {
            response.Dispose();
            yield break;
        }

#if DEBUG
        var sseChunkIndex = 0;
        var sseStreamStartMs = Stopwatch.GetTimestamp();
        DebugLogger.LogSse("open", $"{baseUrl}/global/event");
#endif

        using (response)
        {
            await using var stream = await response.Content
                .ReadAsStreamAsync(cancellationToken)
                .ConfigureAwait(false);

            using var reader = new System.IO.StreamReader(stream);

            string? eventType = null;
            string? eventId = null;
            var dataLines = new StringBuilder();

            // pendingEvent holds a fully-parsed event ready to yield.
            // We cannot yield inside a try/catch block in C# iterators,
            // so we stage the event here and yield it outside the try.
            OpencodeEventDto? pendingEvent = null;
#if DEBUG
            string? pendingChunkData = null;
#endif

            while (!cancellationToken.IsCancellationRequested)
            {
                // Read the next line — catch exceptions here so we can log them
                // before the iterator exits.
                string? line;
                try
                {
                    line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
#if DEBUG
                    var closeDurationMs = (long)((Stopwatch.GetTimestamp() - sseStreamStartMs) * 1000.0 / Stopwatch.Frequency);
                    DebugLogger.LogSse("close", null, totalChunks: sseChunkIndex, streamDurationMs: closeDurationMs);
#endif
                    yield break;
                }
                catch (Exception ex)
                {
#if DEBUG
                    DebugLogger.LogSse("error", ex.Message);
#endif
                    throw;
                }

                if (line is null)
                    break; // End of stream

                if (line.StartsWith("event:", StringComparison.Ordinal))
                {
                    eventType = line["event:".Length..].Trim();
                }
                else if (line.StartsWith("id:", StringComparison.Ordinal))
                {
                    eventId = line["id:".Length..].Trim();
                }
                else if (line.StartsWith("data:", StringComparison.Ordinal))
                {
                    var dataChunk = line["data:".Length..].Trim();
                    dataLines.AppendLine(dataChunk);
                }
                else if (line.Length == 0 && (eventType is not null || dataLines.Length > 0))
                {
                    // Blank line signals end of event — dispatch it
                    JsonElement? data = null;
                    var dataStr = dataLines.ToString().Trim();
#if DEBUG
                    pendingChunkData = dataStr;
#endif

                    if (!string.IsNullOrEmpty(dataStr))
                    {
                        try
                        {
                            data = JsonSerializer.Deserialize<JsonElement>(dataStr);
                        }
                        catch
                        {
                            // Ignore malformed JSON data — surface the event without data
                        }
                    }

                    pendingEvent = new OpencodeEventDto(
                        EventType: eventType ?? "unknown",
                        EventId: eventId,
                        Data: data);

                    eventType = null;
                    eventId = null;
                    dataLines.Clear();
                }

                // Yield the staged event outside any try/catch block (C# iterator constraint).
                if (pendingEvent is not null)
                {
#if DEBUG
                    sseChunkIndex++;
                    DebugLogger.LogSse("chunk", pendingChunkData, chunkIndex: sseChunkIndex);
                    pendingChunkData = null;
#endif
                    yield return pendingEvent;
                    pendingEvent = null;
                }
            }
        }

#if DEBUG
        var finalDurationMs = (long)((Stopwatch.GetTimestamp() - sseStreamStartMs) * 1000.0 / Stopwatch.Frequency);
        DebugLogger.LogSse("close", null, totalChunks: sseChunkIndex, streamDurationMs: finalDurationMs);
#endif
    }
}
