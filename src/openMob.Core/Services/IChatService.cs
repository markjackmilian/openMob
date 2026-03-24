using openMob.Core.Infrastructure.Http.Dtos.Opencode;
using openMob.Core.Models;

namespace openMob.Core.Services;

/// <summary>
/// Service layer for chat operations. Sits between ViewModels and <see cref="IOpencodeApiClient"/>.
/// Provides typed SSE events, HTTP resilience, and automatic SSE reconnect.
/// </summary>
/// <remarks>
/// <para>
/// All operations return <see cref="ChatServiceResult{T}"/> — they never throw for expected
/// errors (network failures, timeouts, circuit breaker open). Callers inspect
/// <see cref="ChatServiceResult{T}.IsSuccess"/> and branch accordingly.
/// </para>
/// <para>
/// <see cref="SubscribeToEventsAsync"/> manages the SSE connection lifecycle including
/// automatic reconnect with exponential backoff and <c>Last-Event-ID</c> resume support.
/// </para>
/// </remarks>
public interface IChatService
{
    // ─── Connection state ─────────────────────────────────────────────────────

    /// <summary>
    /// Gets a value indicating whether the SSE stream is active and connected.
    /// Becomes <c>true</c> after the first <c>server.connected</c> event is received,
    /// and <c>false</c> when the stream is interrupted or the maximum reconnect attempts
    /// are exhausted.
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Raised whenever <see cref="IsConnected"/> changes.
    /// Subscribers receive the new value.
    /// </summary>
    event Action<bool>? IsConnectedChanged;

    // ─── Operations ───────────────────────────────────────────────────────────

    /// <summary>
    /// Sends a plain-text prompt to the given session asynchronously (fire-and-forget).
    /// The AI response arrives via the SSE stream (<see cref="SubscribeToEventsAsync"/>).
    /// </summary>
    /// <remarks>
    /// Returns <see cref="ChatServiceResult{T}.Ok"/> with <c>true</c> on HTTP 204.
    /// Returns <see cref="ChatServiceResult{T}.Fail"/> on network error, timeout, or circuit open.
    /// </remarks>
    /// <param name="sessionId">The session to send the prompt to.</param>
    /// <param name="text">The plain text content of the prompt.</param>
    /// <param name="modelId">The model ID to use, or <c>null</c> for the session default.</param>
    /// <param name="providerId">The provider ID to use, or <c>null</c> for the session default.</param>
    /// <param name="agentName">The agent name to use, or <c>null</c> for the project default.
    /// When <c>null</c>, the <c>"agent"</c> field is omitted from the request body so the
    /// server-side default is preserved.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A <see cref="ChatServiceResult{T}"/> wrapping <c>true</c> on success,
    /// or a <see cref="ChatServiceError"/> on failure.
    /// </returns>
    Task<ChatServiceResult<bool>> SendPromptAsync(
        string sessionId,
        string text,
        string? modelId,
        string? providerId,
        string? agentName = null,
        CancellationToken ct = default);

    /// <summary>
    /// Retrieves the message history for a session.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="limit">Maximum number of messages to return, or <c>null</c> for all.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A <see cref="ChatServiceResult{T}"/> wrapping the list of messages on success,
    /// or a <see cref="ChatServiceError"/> on failure.
    /// </returns>
    Task<ChatServiceResult<IReadOnlyList<MessageWithPartsDto>>> GetMessagesAsync(
        string sessionId,
        int? limit = null,
        CancellationToken ct = default);

    /// <summary>
    /// Opens the SSE event stream with automatic reconnect.
    /// Yields typed <see cref="ChatEvent"/> instances as they arrive.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The stream completes when <paramref name="ct"/> is cancelled or the maximum
    /// number of consecutive reconnect attempts (10) is exhausted without receiving
    /// any events.
    /// </para>
    /// <para>
    /// On reconnect, the <c>Last-Event-ID</c> header is included if a previous event ID
    /// was received, allowing the server to resume the stream from the last known position.
    /// </para>
    /// </remarks>
    /// <param name="ct">Cancellation token. Cancel to stop the stream.</param>
    /// <returns>An async sequence of typed <see cref="ChatEvent"/> objects.</returns>
    IAsyncEnumerable<ChatEvent> SubscribeToEventsAsync(CancellationToken ct = default);
}
