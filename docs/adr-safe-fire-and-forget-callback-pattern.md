# ADR: Safe Fire-and-Forget Pattern for Action<T> Popup Callbacks

## Date
2026-03-21

## Status
Accepted

## Context
`IAppPopupService.ShowModelPickerAsync` accepts an `Action<string>` callback (`onModelSelected`) that is invoked when the user selects a model. However, the callback in `ServerDetailViewModel.ChangeDefaultModelAsync` needs to perform async work: saving the selected model to the database via `IServerConnectionRepository.SetDefaultModelAsync`.

Passing an `async` lambda to `Action<string>` creates an **async void** delegate — a well-known anti-pattern in C#. If the async work throws, the exception is unobserved and crashes the process or is silently swallowed.

## Decision
Use a **safe fire-and-forget** pattern: the `Action<string>` callback invokes a named `async Task` method via `_ = SafeMethodAsync(arg)`, where the method wraps all async work in a try/catch with Sentry error reporting.

```csharp
onModelSelected: (modelId) =>
{
    _ = SafeSetDefaultModelAsync(modelId);
}

private async Task SafeSetDefaultModelAsync(string modelId)
{
    try
    {
        // async work here
    }
    catch (Exception ex)
    {
        SentryHelper.CaptureException(ex, ...);
    }
}
```

## Rationale
- **Changing `IAppPopupService` to accept `Func<string, Task>`** would be the ideal fix but requires updating all call sites across the codebase (ModelPickerSheet, AgentPickerSheet, etc.) and is a larger refactor.
- The safe fire-and-forget pattern is a pragmatic compromise: it prevents crashes, ensures errors are logged, and works within the existing API contract.
- The named method (`SafeSetDefaultModelAsync`) makes the intent explicit and is testable.

## Alternatives Considered
- **Change `IAppPopupService` to `Func<string, Task>`**: Correct but high-impact refactor across all popup consumers. Deferred to a future cleanup spec.
- **Inline try/catch in async lambda**: Works but creates an anonymous async void — harder to test and debug.
- **Synchronous callback with no DB work**: Would require a different architecture (e.g., reading `SelectedModelId` after popup closes). Not feasible with the current popup API which doesn't return a result.

## Consequences
### Positive
- No async void delegates — all exceptions are caught
- Errors reported to Sentry for observability
- Works within existing `Action<string>` API contract
- Named method is testable

### Negative / Trade-offs
- Fire-and-forget means the caller doesn't await completion
- If the DB save fails, the UI may show stale data (mitigated by Sentry alerting)
- The underlying `Action<string>` API limitation remains — a future spec should migrate to `Func<string, Task>`

## Related Features
onboarding-wizard-default-model-step

## Related Agents
om-mobile-core (implementation), om-reviewer (identified the issue)
