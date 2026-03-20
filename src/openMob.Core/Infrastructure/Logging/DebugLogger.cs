using System.Diagnostics;
using System.Text.Json;

namespace openMob.Core.Infrastructure.Logging;

/// <summary>
/// Provides structured debug logging for all critical layers of the openMob app.
/// All members are compiled only in DEBUG builds — zero overhead in Release.
/// </summary>
/// <remarks>
/// Log output is written via <see cref="WriteAction"/>, a static delegate wired at app startup
/// in <c>MauiProgram.cs</c> (Android only, inside <c>#if DEBUG &amp;&amp; ANDROID</c>).
/// The delegate defaults to a no-op so Core never crashes if not wired.
/// </remarks>
public static class DebugLogger
{
#if DEBUG
    /// <summary>
    /// Gets or sets the write action used to emit log entries.
    /// Receives <c>(tag, jsonPayload)</c> where <c>tag</c> is the <c>OM_*</c> layer prefix
    /// and <c>jsonPayload</c> is a single-line JSON string.
    /// Defaults to a no-op. Wire to <c>Android.Util.Log.Debug</c> in <c>MauiProgram.cs</c>.
    /// </summary>
    public static Action<string, string> WriteAction { get; set; } = (_, _) => { };

    /// <summary>
    /// Logs a completed HTTP request/response cycle.
    /// </summary>
    /// <param name="method">The HTTP verb (e.g. <c>"GET"</c>, <c>"POST"</c>).</param>
    /// <param name="url">The full request URL.</param>
    /// <param name="requestBody">The serialised request body, or <c>null</c> if none.</param>
    /// <param name="statusCode">The HTTP response status code.</param>
    /// <param name="responseBody">The response body as a string, or <c>null</c> if none.</param>
    /// <param name="durationMs">The total round-trip duration in milliseconds.</param>
    public static void LogHttp(string method, string url, string? requestBody, int statusCode, string? responseBody, long durationMs)
    {
        var payload = JsonSerializer.Serialize(new
        {
            ts = Now(),
            tid = Tid(),
            layer = "HTTP",
            method,
            url,
            req_body = requestBody,
            status = statusCode,
            res_body = responseBody,
            duration_ms = durationMs,
        });
        WriteAction("OM_HTTP", payload);
    }

    /// <summary>
    /// Logs an SSE (Server-Sent Events) stream lifecycle event or data chunk.
    /// </summary>
    /// <param name="eventType">
    /// One of: <c>"open"</c> (stream started), <c>"chunk"</c> (data received),
    /// <c>"close"</c> (stream ended normally), <c>"error"</c> (exception occurred).
    /// </param>
    /// <param name="chunk">
    /// For <c>"chunk"</c>: the serialised event data string.
    /// For <c>"open"</c>: the stream URL.
    /// For <c>"error"</c>: the exception message.
    /// For <c>"close"</c>: <c>null</c>.
    /// </param>
    /// <param name="chunkIndex">
    /// For <c>"chunk"</c>: the 1-based index of this chunk within the stream.
    /// Tracked by the caller (<see cref="OpencodeApiClient"/>) as a local variable per stream.
    /// Ignored for other event types.
    /// </param>
    /// <param name="totalChunks">
    /// For <c>"close"</c>: the total number of chunks received in this stream.
    /// Ignored for other event types.
    /// </param>
    /// <param name="streamDurationMs">
    /// For <c>"close"</c>: the total stream duration in milliseconds.
    /// Ignored for other event types.
    /// </param>
    public static void LogSse(
        string eventType,
        string? chunk,
        int chunkIndex = 0,
        int totalChunks = 0,
        long streamDurationMs = 0)
    {
        string payload;

        switch (eventType)
        {
            case "open":
                payload = JsonSerializer.Serialize(new
                {
                    ts = Now(),
                    tid = Tid(),
                    layer = "SSE",
                    @event = eventType,
                    url = chunk,
                });
                break;

            case "chunk":
                payload = JsonSerializer.Serialize(new
                {
                    ts = Now(),
                    tid = Tid(),
                    layer = "SSE",
                    @event = eventType,
                    chunk_index = chunkIndex,
                    content = chunk,
                });
                break;

            case "close":
                payload = JsonSerializer.Serialize(new
                {
                    ts = Now(),
                    tid = Tid(),
                    layer = "SSE",
                    @event = eventType,
                    total_chunks = totalChunks,
                    total_ms = streamDurationMs,
                });
                break;

            case "error":
                payload = JsonSerializer.Serialize(new
                {
                    ts = Now(),
                    tid = Tid(),
                    layer = "SSE",
                    @event = eventType,
                    error = chunk,
                });
                break;

            default:
                payload = JsonSerializer.Serialize(new
                {
                    ts = Now(),
                    tid = Tid(),
                    layer = "SSE",
                    @event = eventType,
                    content = chunk,
                });
                break;
        }

        WriteAction("OM_SSE", payload);
    }

    /// <summary>
    /// Logs a ViewModel command lifecycle event.
    /// </summary>
    /// <param name="commandName">The name of the command method (use <c>nameof</c>).</param>
    /// <param name="phase">
    /// One of: <c>"start"</c>, <c>"complete"</c>, <c>"failed"</c>.
    /// </param>
    /// <param name="durationMs">
    /// For <c>"complete"</c>: the elapsed execution time in milliseconds.
    /// For <c>"start"</c> and <c>"failed"</c>: <c>0</c> (unused).
    /// </param>
    /// <param name="error">
    /// For <c>"failed"</c>: the full exception message and stack trace.
    /// For <c>"start"</c> and <c>"complete"</c>: <c>null</c>.
    /// </param>
    public static void LogCommand(string commandName, string phase, long durationMs = 0, string? error = null)
    {
        string payload;

        switch (phase)
        {
            case "complete":
                payload = JsonSerializer.Serialize(new
                {
                    ts = Now(),
                    tid = Tid(),
                    layer = "CMD",
                    command = commandName,
                    phase,
                    duration_ms = durationMs,
                });
                break;

            case "failed":
                payload = JsonSerializer.Serialize(new
                {
                    ts = Now(),
                    tid = Tid(),
                    layer = "CMD",
                    command = commandName,
                    phase,
                    error,
                });
                break;

            default: // "start" and any other phase
                payload = JsonSerializer.Serialize(new
                {
                    ts = Now(),
                    tid = Tid(),
                    layer = "CMD",
                    command = commandName,
                    phase,
                });
                break;
        }

        WriteAction("OM_CMD", payload);
    }

    /// <summary>
    /// Logs a Shell navigation event.
    /// </summary>
    /// <param name="route">The navigation route string (e.g. <c>"//chat"</c>, <c>".."</c>).</param>
    /// <param name="parameters">Optional navigation parameters. Serialised to JSON if non-null.</param>
    public static void LogNavigation(string route, object? parameters = null)
    {
        var payload = JsonSerializer.Serialize(new
        {
            ts = Now(),
            tid = Tid(),
            layer = "NAV",
            route,
            @params = parameters is not null ? JsonSerializer.Serialize(parameters) : null,
        });
        WriteAction("OM_NAV", payload);
    }

    /// <summary>
    /// Logs a database repository operation.
    /// </summary>
    /// <param name="operation">The operation type (e.g. <c>"GetAll"</c>, <c>"Add"</c>, <c>"Update"</c>, <c>"Delete"</c>, <c>"SetActive"</c>).</param>
    /// <param name="entity">The entity name (e.g. <c>"ServerConnection"</c>).</param>
    /// <param name="keyInfo">The entity key or filter description, or <c>null</c> if not applicable.</param>
    /// <param name="durationMs">The operation duration in milliseconds.</param>
    public static void LogDatabase(string operation, string entity, string? keyInfo, long durationMs)
    {
        var payload = JsonSerializer.Serialize(new
        {
            ts = Now(),
            tid = Tid(),
            layer = "DB",
            op = operation,
            entity,
            key = keyInfo,
            duration_ms = durationMs,
        });
        WriteAction("OM_DB", payload);
    }

    /// <summary>
    /// Logs a server connection management event.
    /// </summary>
    /// <param name="eventType">
    /// One of: <c>"health_check"</c>, <c>"server_changed"</c>, <c>"discovery_result"</c>.
    /// </param>
    /// <param name="detail">Optional detail string describing the event outcome.</param>
    /// <param name="durationMs">Optional duration in milliseconds (used for health checks).</param>
    public static void LogConnection(string eventType, string? detail = null, long durationMs = 0)
    {
        var payload = JsonSerializer.Serialize(new
        {
            ts = Now(),
            tid = Tid(),
            layer = "CONN",
            @event = eventType,
            detail,
            duration_ms = durationMs,
        });
        WriteAction("OM_CONN", payload);
    }

    /// <summary>Returns the current UTC timestamp in ISO 8601 format with millisecond precision.</summary>
    private static string Now() => DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");

    /// <summary>Returns the current managed thread ID.</summary>
    private static int Tid() => Environment.CurrentManagedThreadId;
#endif
}
