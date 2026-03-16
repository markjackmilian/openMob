using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using openMob.Core.Helpers;
using openMob.Core.Infrastructure.Http;
using openMob.Core.Infrastructure.Http.Dtos.Opencode;
using openMob.Core.Models;

namespace openMob.Core.Services;

/// <summary>
/// Default implementation of <see cref="IChatService"/>.
/// Delegates HTTP calls to <see cref="IOpencodeApiClient"/> and manages the SSE
/// connection lifecycle with automatic reconnect and <c>Last-Event-ID</c> resume.
/// </summary>
/// <remarks>
/// <para>
/// Registered as Singleton because it maintains shared state: <see cref="IsConnected"/>
/// and the last received SSE event ID. Thread-safety is ensured via <c>volatile</c> for
/// the boolean flag and a dedicated lock object for the string field.
/// </para>
/// <para>
/// <see cref="IOpencodeApiClient"/> is injected directly (captured Transient in Singleton).
/// This is intentional: <see cref="ChatService"/> is the sole consumer of the SSE stream
/// and the prompt/message calls, so a single captured instance is acceptable.
/// </para>
/// <para>
/// The SSE connection uses the <c>"opencode-sse"</c> named client (registered without a
/// resilience pipeline and with <see cref="System.Threading.Timeout.InfiniteTimeSpan"/>)
/// to avoid the 30-second request timeout that would terminate long-lived SSE connections.
/// </para>
/// </remarks>
internal sealed class ChatService : IChatService
{
    private const int MaxConsecutiveFailedAttempts = 10;
    private const int InitialBackoffMs = 1_000;
    private const int MaxBackoffMs = 30_000;

    private readonly IOpencodeApiClient _apiClient;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOpencodeConnectionManager _connectionManager;

    // IsConnected state — stored as volatile int (0 = false, 1 = true) so that
    // Interlocked.Exchange can make the guard+assign in SetConnected atomic.
    private volatile int _isConnected; // 0 = false, 1 = true

    // Last-Event-ID — protected by a lock because string assignment is not atomic on all platforms
    private string? _lastEventId;
    private readonly object _lastEventIdLock = new();

    /// <summary>
    /// Initialises the service with the required dependencies.
    /// </summary>
    /// <param name="apiClient">The opencode API client for HTTP calls.</param>
    /// <param name="httpClientFactory">Factory for creating the SSE-specific HTTP client.</param>
    /// <param name="connectionManager">Resolves the active server base URL and auth header.</param>
    public ChatService(
        IOpencodeApiClient apiClient,
        IHttpClientFactory httpClientFactory,
        IOpencodeConnectionManager connectionManager)
    {
        _apiClient = apiClient;
        _httpClientFactory = httpClientFactory;
        _connectionManager = connectionManager;
    }

    /// <inheritdoc />
    public bool IsConnected => _isConnected == 1;

    /// <inheritdoc />
    public event Action<bool>? IsConnectedChanged;

    // ─── Private helpers ──────────────────────────────────────────────────────

    private void SetConnected(bool value)
    {
        var newVal = value ? 1 : 0;
        var oldVal = Interlocked.Exchange(ref _isConnected, newVal);
        if (oldVal != newVal)
            IsConnectedChanged?.Invoke(value);
    }

    /// <summary>
    /// Maps an <see cref="ErrorKind"/> from the API layer to a <see cref="ChatServiceErrorKind"/>.
    /// </summary>
    private static ChatServiceErrorKind MapErrorKind(ErrorKind kind) =>
        kind switch
        {
            ErrorKind.Timeout => ChatServiceErrorKind.Timeout,
            ErrorKind.NetworkUnreachable => ChatServiceErrorKind.NetworkError,
            ErrorKind.ServerError => ChatServiceErrorKind.ServerError,
            // 401 Unauthorized and 404 Not Found are server-side rejections — map to ServerError
            // so the ViewModel can surface a meaningful "server refused the request" message.
            ErrorKind.Unauthorized => ChatServiceErrorKind.ServerError,
            ErrorKind.NotFound => ChatServiceErrorKind.ServerError,
            // NoActiveServer means no reachable server was found — treat as a network-level failure.
            ErrorKind.NoActiveServer => ChatServiceErrorKind.NetworkError,
            _ => ChatServiceErrorKind.Unknown,
        };

    // ─── SendPromptAsync ──────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<ChatServiceResult<bool>> SendPromptAsync(
        string sessionId,
        string text,
        string? modelId,
        string? providerId,
        CancellationToken ct = default)
    {
        try
        {
            var request = SendPromptRequestBuilder.FromText(text, modelId, providerId);
            var result = await _apiClient.SendPromptAsyncNoWait(sessionId, request, ct)
                .ConfigureAwait(false);

            if (result.IsSuccess)
                return ChatServiceResult<bool>.Ok(true);

            var error = result.Error!;
            return ChatServiceResult<bool>.Fail(new ChatServiceError(
                Kind: MapErrorKind(error.Kind),
                Message: error.Message,
                HttpStatusCode: error.HttpStatusCode));
        }
        catch (Exception ex) when (IsCircuitBreakerException(ex))
        {
            return ChatServiceResult<bool>.Fail(new ChatServiceError(
                Kind: ChatServiceErrorKind.CircuitOpen,
                Message: $"Circuit breaker is open. Requests are temporarily blocked: {ex.Message}"));
        }
        catch (OperationCanceledException)
        {
            return ChatServiceResult<bool>.Fail(new ChatServiceError(
                Kind: ChatServiceErrorKind.Cancelled,
                Message: "The operation was cancelled."));
        }
        catch (Exception ex)
        {
            return ChatServiceResult<bool>.Fail(new ChatServiceError(
                Kind: ChatServiceErrorKind.Unknown,
                Message: $"An unexpected error occurred: {ex.Message}"));
        }
    }

    // ─── GetMessagesAsync ─────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<ChatServiceResult<IReadOnlyList<MessageWithPartsDto>>> GetMessagesAsync(
        string sessionId,
        int? limit = null,
        CancellationToken ct = default)
    {
        try
        {
            var result = await _apiClient.GetMessagesAsync(sessionId, limit, ct)
                .ConfigureAwait(false);

            if (result.IsSuccess)
                return ChatServiceResult<IReadOnlyList<MessageWithPartsDto>>.Ok(result.Value!);

            var error = result.Error!;
            return ChatServiceResult<IReadOnlyList<MessageWithPartsDto>>.Fail(new ChatServiceError(
                Kind: MapErrorKind(error.Kind),
                Message: error.Message,
                HttpStatusCode: error.HttpStatusCode));
        }
        catch (Exception ex) when (IsCircuitBreakerException(ex))
        {
            return ChatServiceResult<IReadOnlyList<MessageWithPartsDto>>.Fail(new ChatServiceError(
                Kind: ChatServiceErrorKind.CircuitOpen,
                Message: $"Circuit breaker is open. Requests are temporarily blocked: {ex.Message}"));
        }
        catch (OperationCanceledException)
        {
            return ChatServiceResult<IReadOnlyList<MessageWithPartsDto>>.Fail(new ChatServiceError(
                Kind: ChatServiceErrorKind.Cancelled,
                Message: "The operation was cancelled."));
        }
        catch (Exception ex)
        {
            return ChatServiceResult<IReadOnlyList<MessageWithPartsDto>>.Fail(new ChatServiceError(
                Kind: ChatServiceErrorKind.Unknown,
                Message: $"An unexpected error occurred: {ex.Message}"));
        }
    }

    // ─── SubscribeToEventsAsync ───────────────────────────────────────────────

    /// <inheritdoc />
    public async IAsyncEnumerable<ChatEvent> SubscribeToEventsAsync(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var consecutiveFailedAttempts = 0;
        var backoffMs = InitialBackoffMs;

        while (!ct.IsCancellationRequested)
        {
            // Collect events from a single SSE connection attempt.
            // We cannot yield inside a try/catch block (C# language restriction), so we
            // use an inner async enumerable and yield its results outside the try/catch.
            var connectionResult = await TryOpenSseConnectionAsync(ct).ConfigureAwait(false);

            if (connectionResult.WasCancelled)
            {
                SetConnected(false);
                yield break;
            }

            // Yield all events from this connection attempt
            foreach (var chatEvent in connectionResult.Events)
            {
                yield return chatEvent;
            }

            // Determine whether to reset or increment the failure counter
            if (connectionResult.ReceivedAtLeastOneEvent)
            {
                consecutiveFailedAttempts = 0;
                backoffMs = InitialBackoffMs;
            }
            else
            {
                consecutiveFailedAttempts++;
            }

            SetConnected(false);

            if (consecutiveFailedAttempts >= MaxConsecutiveFailedAttempts)
            {
                // Maximum reconnect attempts exhausted without receiving any events
                yield break;
            }

            // Exponential backoff before reconnect: 1s, 2s, 4s, ... capped at 30s
            try
            {
                await Task.Delay(backoffMs, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                SetConnected(false);
                yield break;
            }

            backoffMs = Math.Min(backoffMs * 2, MaxBackoffMs);
        }

        SetConnected(false);
    }

    /// <summary>
    /// Holds the result of a single SSE connection attempt.
    /// </summary>
    private sealed class SseConnectionResult
    {
        /// <summary>Gets the events received during this connection attempt.</summary>
        public IReadOnlyList<ChatEvent> Events { get; init; } = [];

        /// <summary>Gets a value indicating whether at least one event was received.</summary>
        public bool ReceivedAtLeastOneEvent { get; init; }

        /// <summary>Gets a value indicating whether the connection was cancelled via the token.</summary>
        public bool WasCancelled { get; init; }
    }

    /// <summary>
    /// Opens a single SSE connection, collects all events until the stream ends or an error occurs,
    /// and returns them as a <see cref="SseConnectionResult"/>.
    /// This method exists to work around the C# restriction that prevents <c>yield return</c>
    /// inside a <c>try/catch</c> block.
    /// </summary>
    private async Task<SseConnectionResult> TryOpenSseConnectionAsync(CancellationToken ct)
    {
        var events = new List<ChatEvent>();

        try
        {
            await foreach (var chatEvent in OpenSseConnectionAsync(ct).ConfigureAwait(false))
            {
                events.Add(chatEvent);
            }

            return new SseConnectionResult
            {
                Events = events,
                ReceivedAtLeastOneEvent = events.Count > 0,
                WasCancelled = false,
            };
        }
        catch (OperationCanceledException)
        {
            return new SseConnectionResult
            {
                Events = events,
                ReceivedAtLeastOneEvent = events.Count > 0,
                WasCancelled = true,
            };
        }
        catch (Exception)
        {
            return new SseConnectionResult
            {
                Events = events,
                ReceivedAtLeastOneEvent = events.Count > 0,
                WasCancelled = false,
            };
        }
    }

    /// <summary>
    /// Opens a single SSE connection and yields parsed <see cref="ChatEvent"/> objects.
    /// Completes when the stream ends or the cancellation token is triggered.
    /// </summary>
    private async IAsyncEnumerable<ChatEvent> OpenSseConnectionAsync(
        [EnumeratorCancellation] CancellationToken ct)
    {
        var baseUrl = await _connectionManager.GetBaseUrlAsync(ct).ConfigureAwait(false);
        if (baseUrl is null)
            yield break;

        var client = _httpClientFactory.CreateClient("opencode-sse");

        var authHeader = await _connectionManager.GetBasicAuthHeaderAsync(ct).ConfigureAwait(false);

        // Include Last-Event-ID header if we have a previous event ID (for resume on reconnect)
        string? lastEventId;
        lock (_lastEventIdLock)
        {
            lastEventId = _lastEventId;
        }

        // Use a per-request HttpRequestMessage instead of mutating DefaultRequestHeaders.
        // DefaultRequestHeaders.TryAddWithoutValidation *appends* a new value on every call —
        // it does not replace — so after N reconnects the client would carry N Last-Event-ID
        // headers. Per-request headers are scoped to this single SendAsync call and are
        // discarded when the using block exits.
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/global/event");

        if (authHeader is not null)
        {
            var encoded = authHeader.StartsWith("Basic ", StringComparison.Ordinal)
                ? authHeader["Basic ".Length..]
                : authHeader;
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", encoded);
        }

        if (lastEventId is not null)
            request.Headers.TryAddWithoutValidation("Last-Event-ID", lastEventId);

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct)
                .ConfigureAwait(false);
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

        using (response)
        {
            await using var stream = await response.Content
                .ReadAsStreamAsync(ct)
                .ConfigureAwait(false);

            await foreach (var rawEvent in ParseSseStream(stream, ct).ConfigureAwait(false))
            {
                // Track the last event ID for reconnect resume
                if (rawEvent.EventId is not null)
                {
                    lock (_lastEventIdLock)
                    {
                        _lastEventId = rawEvent.EventId;
                    }
                }

                var chatEvent = ChatEventParser.Parse(rawEvent);

                if (chatEvent is ServerConnectedEvent)
                    SetConnected(true);

                yield return chatEvent;
            }
        }
    }

    /// <summary>
    /// Parses an SSE stream line-by-line and yields <see cref="OpencodeEventDto"/> objects.
    /// Handles the SSE wire format: <c>event:</c>, <c>id:</c>, <c>data:</c> fields
    /// separated by blank lines.
    /// </summary>
    /// <param name="stream">The raw HTTP response stream.</param>
    /// <param name="ct">Cancellation token.</param>
    private static async IAsyncEnumerable<OpencodeEventDto> ParseSseStream(
        System.IO.Stream stream,
        [EnumeratorCancellation] CancellationToken ct)
    {
        using var reader = new System.IO.StreamReader(stream);

        string? eventType = null;
        string? eventId = null;
        var dataLines = new StringBuilder();

        while (!ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct).ConfigureAwait(false);
            if (line is null)
                yield break; // End of stream

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
                dataLines.AppendLine(line["data:".Length..].Trim());
            }
            else if (line.Length == 0 && (eventType is not null || dataLines.Length > 0))
            {
                // Blank line signals end of event — dispatch it
                JsonElement? data = null;
                var dataStr = dataLines.ToString().Trim();

                if (!string.IsNullOrEmpty(dataStr))
                {
                    try
                    {
                        data = JsonSerializer.Deserialize<JsonElement>(dataStr);
                    }
                    catch
                    {
                        // Ignore malformed JSON — surface the event without data
                    }
                }

                yield return new OpencodeEventDto(
                    EventType: eventType ?? "unknown",
                    EventId: eventId,
                    Data: data);

                eventType = null;
                eventId = null;
                dataLines.Clear();
            }
        }
    }

    /// <summary>
    /// Determines whether an exception is a Polly circuit breaker exception.
    /// Uses type name matching to avoid a hard compile-time dependency on Polly types
    /// before the NuGet package is fully resolved by the LSP.
    /// </summary>
    /// <remarks>
    /// At runtime, <c>Microsoft.Extensions.Http.Resilience</c> brings in Polly transitively.
    /// The actual types are <c>Polly.CircuitBreaker.BrokenCircuitException</c> and
    /// <c>Polly.CircuitBreaker.IsolatedCircuitException</c>.
    /// </remarks>
    private static bool IsCircuitBreakerException(Exception ex)
    {
        var typeName = ex.GetType().FullName ?? string.Empty;
        return typeName.Contains("BrokenCircuitException", StringComparison.Ordinal)
            || typeName.Contains("IsolatedCircuitException", StringComparison.Ordinal)
            || typeName.Contains("ExecutionRejectedException", StringComparison.Ordinal);
    }
}
