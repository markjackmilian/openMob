# ADR: WeakReferenceMessenger for Cross-ViewModel Communication

## Date
2026-03-20

## Status
Accepted

## Context

The `ContextSheetViewModel` (bottom sheet) needs to notify `ChatViewModel` (parent page) when a project preference changes — specifically when the user selects a new model, agent, thinking level, or auto-accept setting. The two ViewModels have no direct reference to each other: `ContextSheetViewModel` is resolved as a new Transient instance each time the sheet opens, while `ChatViewModel` is a longer-lived Transient tied to the chat page lifecycle.

Two options were considered for propagating changes:

1. **Callback/delegate pattern** — pass an `Action<ProjectPreference>` callback from `ChatViewModel` into `ContextSheetViewModel` via `IAppPopupService.ShowContextSheetAsync`. This is the pattern used by `ShowModelPickerAsync`.
2. **WeakReferenceMessenger** — publish a `ProjectPreferenceChangedMessage` from `ContextSheetViewModel`; `ChatViewModel` subscribes in its constructor.

The spec explicitly resolved this question (Open Question #1): use `WeakReferenceMessenger.Default` directly, without changing `ChatViewModel`'s base class to `ObservableRecipient`.

## Decision

Use `CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default` for cross-ViewModel communication between `ContextSheetViewModel` and `ChatViewModel`.

- `ContextSheetViewModel` **sends** `ProjectPreferenceChangedMessage` after every successful preference save.
- `ChatViewModel` **registers** a handler in its constructor and **unregisters** in `Dispose()`.
- No base class change: `ChatViewModel` remains `ObservableObject`, not `ObservableRecipient`.

## Rationale

The callback pattern (`Action<T>`) works well for single-selection pickers (model picker, agent picker) where the result is a single value returned once. For the Context Sheet, multiple independent preferences can change during a single sheet session, and the sheet may remain open for an extended period. A callback would need to be invoked multiple times with partial state, or the full `ProjectPreference` object would need to be passed back on every change — coupling the callback signature to the entity.

`WeakReferenceMessenger` decouples the sender from the receiver entirely. `ContextSheetViewModel` does not need to know that `ChatViewModel` exists. Future subscribers (e.g., a `ContextStatusBarViewModel`) can register without any changes to `ContextSheetViewModel`.

The `ObservableRecipient` base class was rejected because `ChatViewModel` is already a complex class with `IDisposable` and SSE lifecycle management. Changing its base class would require adding `IsActive` lifecycle coupling and testing the activation/deactivation paths — unnecessary complexity for a straightforward subscription.

## Alternatives Considered

- **`Action<ProjectPreference>` callback via `ShowContextSheetAsync`**: Rejected — requires the caller to manage a callback that fires multiple times for different preference types. Couples `IAppPopupService` to the `ProjectPreference` entity.
- **`ObservableRecipient` base class on `ChatViewModel`**: Rejected — requires `IsActive` lifecycle management, adds complexity to an already large ViewModel, and was explicitly ruled out in the spec's Open Questions.
- **Direct ViewModel reference injection**: Rejected — creates tight coupling between two Transient ViewModels with different lifetimes.
- **Event on `IProjectPreferenceService`**: Rejected — services should not raise UI events; this would violate the layer separation rule.

## Consequences

### Positive
- `ContextSheetViewModel` is fully decoupled from `ChatViewModel`.
- Future ViewModels can subscribe to `ProjectPreferenceChangedMessage` without modifying `ContextSheetViewModel`.
- The message type (`ProjectPreferenceChangedMessage`) is a plain sealed record — easy to test by sending directly via `WeakReferenceMessenger.Default.Send(...)` in unit tests.
- `WeakReferenceMessenger` holds weak references to subscribers — no memory leak if `ChatViewModel` is garbage-collected without calling `Dispose()`.

### Negative / Trade-offs
- Developers must remember to call `WeakReferenceMessenger.Default.UnregisterAll(this)` in `Dispose()`. Forgetting this causes stale handlers to receive messages after the ViewModel is logically dead (though the weak reference prevents memory leaks, the handler may still execute on a disposed object).
- Message flow is implicit — a developer reading `ContextSheetViewModel` cannot immediately see who receives the message without searching for `Register<ProjectPreferenceChangedMessage>` across the codebase.
- Test isolation requires explicit cleanup: test classes that send `ProjectPreferenceChangedMessage` must call `WeakReferenceMessenger.Default.UnregisterAll(this)` in their `Dispose()` method to prevent cross-test pollution.

## Implementation Notes

**Sending (ContextSheetViewModel):**
```csharp
WeakReferenceMessenger.Default.Send(
    new ProjectPreferenceChangedMessage(projectId, updatedPref));
```

**Receiving (ChatViewModel constructor):**
```csharp
WeakReferenceMessenger.Default.Register<ProjectPreferenceChangedMessage>(
    this,
    (_, message) =>
    {
        if (message.ProjectId != CurrentProjectId) return;
        // update SelectedModelId, SelectedModelName, etc.
    });
```

**Cleanup (ChatViewModel.Dispose — must be first line):**
```csharp
public void Dispose()
{
    WeakReferenceMessenger.Default.UnregisterAll(this);
    // ... rest of cleanup
}
```

**Test pattern:**
```csharp
// Subscribe in test
ProjectPreferenceChangedMessage? received = null;
WeakReferenceMessenger.Default.Register<ProjectPreferenceChangedMessage>(
    this, (_, msg) => received = msg);

// Trigger the change, wait for fire-and-forget
await Task.Delay(200);

// Assert
received.Should().NotBeNull();

// Cleanup
WeakReferenceMessenger.Default.UnregisterAll(this);
```

## Related Features
- session-context-sheet-1of3-core
- session-context-sheet-2of3-agent-model (future — will add agent name to the message handler)
- session-context-sheet-3of3-thinking-autoaccept-subagent (future — will add ThinkingLevel/AutoAccept to the message handler)

## Related Agents
- om-mobile-core (implements senders and receivers)
- om-tester (must clean up messenger registrations in test teardown)
