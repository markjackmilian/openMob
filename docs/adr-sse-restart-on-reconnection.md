# ADR: Explicitly Restart SSE Subscription After Successful Reconnection

## Date
2026-03-31

## Status
Accepted

## Context
The SSE subscription in `ChatViewModel` is an `await foreach` loop over `_chatService.SubscribeToEventsAsync(ct)`. When the server goes offline, the underlying HTTP connection drops and the loop exits — either with an exception (caught silently) or by the stream completing.

The reconnection modal uses `IOpencodeConnectionManager.IsServerReachableAsync()` to probe the server. When this returns `true`, the modal closes and the heartbeat monitor is reset. However, `IsServerReachableAsync` only confirms HTTP reachability — it does not re-establish the SSE stream. Without an explicit restart, no new SSE events (including heartbeats) arrive. After 60 seconds, the heartbeat monitor transitions back to `Lost` and the modal reappears, creating an infinite reconnection loop.

## Decision
In the `ReconnectionSucceeded` event handler in `ChatViewModel.OnHealthStateChanged`, after resetting the heartbeat monitor and closing the modal, call `StartSseSubscriptionAsync()` if a session is active:

```csharp
if (CurrentSessionId is not null)
    _ = StartSseSubscriptionAsync();
```

`StartSseSubscriptionAsync` cancels any stale `_sseCts` before opening a new stream, so it is safe to call unconditionally when a session is active.

## Rationale
The SSE loop is not self-healing — it exits on connection drop and does not retry automatically. The reconnection modal already confirms the server is reachable via HTTP; restarting SSE immediately after confirmation is the correct point to resume real-time event delivery. `StartSseSubscriptionAsync` is already idempotent (cancels previous subscription before starting a new one).

## Alternatives Considered
- **Auto-retry inside `StartSseSubscriptionAsync`**: Add a retry loop in the SSE method itself. Rejected — conflates the reconnection UX (modal, backoff, user action) with the transport layer.
- **Restart SSE on every `OnAppearing`**: Would interrupt active streams unnecessarily (e.g. when returning from the flyout).
- **Use `IOpencodeConnectionManager.StatusChanged`**: Not triggered by SSE stream drops; reflects the connection manager's own health checks only.

## Consequences
### Positive
- After reconnection, the app immediately resumes receiving SSE events (heartbeats, message updates, etc.).
- The reconnection modal does not reappear after a successful reconnection.

### Negative / Trade-offs
- If the server drops again immediately after reconnection, the SSE loop exits again and the monitor will eventually transition back to `Lost`. This is the correct behaviour — the modal will reappear.

## Related Features
heartbeat-monitor-footer

## Related Agents
om-mobile-core (ChatViewModel, StartSseSubscriptionAsync)
