# Sentry Error Classification — Expected vs Unexpected Errors

## Metadata
| Field   | Value                        |
|---------|------------------------------|
| Date    | 2026-03-24                   |
| Status  | Draft                        |
| Version | 1.0                          |

---

## Executive Summary

The app currently sends Sentry events for `OpencodeResult` failures that represent **expected, non-exceptional conditions** — such as `ErrorKind.NoActiveServer` when no server has been configured yet (e.g. first launch). This pollutes the Sentry dashboard with noise and makes it harder to identify real bugs. The fix is to centralise error classification so that only truly unexpected errors (`ErrorKind.Unknown` and unstructured exceptions) are ever forwarded to Sentry, while all expected API error kinds are silently discarded (with optional local debug logging).

---

## Scope

### In Scope
- Define a canonical list of `ErrorKind` values that are **expected** and must never be sent to Sentry.
- Introduce a centralised filtering mechanism (in `SentryHelper` or a dedicated helper) so the rule is enforced in one place, not duplicated across services.
- Fix all service-layer call sites that today wrap `result.Error` in `new InvalidOperationException(...)` and pass it to `SentryHelper.CaptureException` without checking `ErrorKind`: `SessionService`, `AgentService`, `ProviderService`, `ProjectService`.
- Unify the already-correct partial filtering (`ErrorKind.NotFound` guard in `SessionService.GetSessionAsync` and `ProjectService.GetCurrentProjectAsync`) under the new centralised rule.
- Log expected errors locally via `Debug.WriteLine` (or equivalent) so they remain observable during development without reaching Sentry.

### Out of Scope
- Changing the structure of `OpencodeResult<T>`, `OpencodeApiError`, or the `ErrorKind` enum.
- Modifying ViewModel-level `catch (Exception ex)` blocks — those catch genuinely unexpected thrown exceptions and are correct as-is.
- Introducing a persistent local log or a new logging infrastructure.
- Changing `ChatServiceResult<T>` / `ChatServiceErrorKind` mapping logic.
- Any UI changes or user-facing error message changes.

---

## Functional Requirements

> Requirements are numbered for traceability.

1. **[REQ-001]** The following `ErrorKind` values are classified as **expected errors** and must never produce a Sentry event:
   - `ErrorKind.NoActiveServer` — no server configured (e.g. first launch, onboarding not completed)
   - `ErrorKind.NetworkUnreachable` — device offline or server unreachable
   - `ErrorKind.Timeout` — request timed out
   - `ErrorKind.Unauthorized` — HTTP 401 (session expired, wrong key)
   - `ErrorKind.NotFound` — HTTP 404 (resource does not exist)
   - `ErrorKind.ServerError` — HTTP 5xx (server-side failure)

2. **[REQ-002]** Only `ErrorKind.Unknown` is classified as an **unexpected error** and must be forwarded to Sentry.

3. **[REQ-003]** A new centralised helper method — `SentryHelper.CaptureOpencodeError(OpencodeApiError error, IDictionary<string, object>? extras)` — must be introduced. Internally it must:
   - Return immediately (no Sentry call) if `error.Kind != ErrorKind.Unknown`.
   - Call `SentrySdk.CaptureException` (wrapping the error in an `InvalidOperationException`) only for `ErrorKind.Unknown`.
   - Emit a `Debug.WriteLine` for every call regardless of kind, so expected errors remain visible in the debug output.

4. **[REQ-004]** All existing service-layer call sites that today call `SentryHelper.CaptureException(new InvalidOperationException(...))` after checking `result.Error is not null` must be migrated to call `SentryHelper.CaptureOpencodeError(result.Error, extras)` instead. Affected files:
   - `SessionService.cs` — 6 call sites (`GetAllSessionsAsync`, `GetSessionAsync`, `CreateSessionAsync`, `CreateSessionForProjectAsync`, `UpdateSessionTitleAsync`, `DeleteSessionAsync`, `ForkSessionAsync`)
   - `AgentService.cs` — 1 call site (`GetAgentsAsync`)
   - `ProviderService.cs` — 3 call sites (`GetProvidersAsync`, `SetProviderAuthAsync`, `GetConfiguredProvidersAsync`)
   - `ProjectService.cs` — 2 call sites (`GetAllProjectsAsync`, `GetCurrentProjectAsync`)

5. **[REQ-005]** The ad-hoc `ErrorKind.NotFound` guards already present in `SessionService.GetSessionAsync` and `ProjectService.GetCurrentProjectAsync` must be removed after migration to `CaptureOpencodeError`, since the centralised method already handles `NotFound` correctly.

6. **[REQ-006]** The `CreateSessionForProjectAsync` method in `SessionService` currently calls `SentryHelper.CaptureException` and then re-throws. After migration, the re-throw must be preserved — only the Sentry call must be replaced with `CaptureOpencodeError`.

7. **[REQ-007]** No changes must be made to ViewModel-level `catch (Exception ex)` blocks. Those blocks catch genuinely unexpected thrown exceptions (not `OpencodeResult` failures) and their Sentry reporting is correct.

---

## Functional Impacts

### Affected Components / Systems
| Component | Impact | Notes |
|-----------|--------|-------|
| `SentryHelper.cs` | New method added | `CaptureOpencodeError(OpencodeApiError, extras?)` |
| `SessionService.cs` | 6–7 call sites migrated | Remove ad-hoc `NotFound` guard after migration |
| `AgentService.cs` | 1 call site migrated | |
| `ProviderService.cs` | 3 call sites migrated | |
| `ProjectService.cs` | 2 call sites migrated | Remove ad-hoc `NotFound` guard after migration |
| Sentry dashboard | Fewer events on first launch and in offline/no-server scenarios | Expected improvement |
| `openMob.Tests` | New unit tests required | See Acceptance Criteria |

### Dependencies
- `Sentry` NuGet package (already present — no new dependency).
- `ErrorKind` enum in `openMob.Core.Infrastructure.Http.OpencodeResult` (read-only dependency — not modified).
- `OpencodeApiError` type (read-only dependency — not modified).

---

## Open Questions & Clarifications

| # | Question | Status | Answer / Decision |
|---|----------|--------|-------------------|
| 1 | Should `ErrorKind.Unauthorized` be sent to Sentry? It could indicate a misconfigured API key (expected) or a revoked token (potentially worth knowing). | Resolved | Not sent to Sentry — classified as expected. The user can reconfigure the key. |
| 2 | Should `ErrorKind.ServerError` (5xx) be sent to Sentry? It originates from the remote opencode server, not from the app itself. | Resolved | Not sent to Sentry — it is a server-side condition outside the app's control. |
| 3 | Should the `Debug.WriteLine` for expected errors include the full error message and `ErrorKind`? | Resolved | Yes — format: `[openMob] Expected API error ({Kind}): {Message}` |
| 4 | Is `ChatServiceResult<T>` / `ChatServiceErrorKind` in scope? | Resolved | Out of scope — `ChatService` does not call `SentryHelper` directly; errors surface via `ChatViewModel` catch blocks which handle thrown exceptions, not result values. |

---

## Acceptance Criteria

> Each criterion maps to one or more functional requirements.

- [ ] **[AC-001]** Given the app is launched for the first time with no server configured, when any service method calls the API and receives `ErrorKind.NoActiveServer`, then no event is sent to Sentry. *(REQ-001, REQ-003, REQ-004)*
- [ ] **[AC-002]** Given an `OpencodeApiError` with `Kind == ErrorKind.Unknown`, when `SentryHelper.CaptureOpencodeError` is called, then exactly one event is sent to Sentry containing the error message and extras. *(REQ-002, REQ-003)*
- [ ] **[AC-003]** Given an `OpencodeApiError` with any `Kind` other than `Unknown` (`NoActiveServer`, `NotFound`, `Timeout`, `NetworkUnreachable`, `Unauthorized`, `ServerError`), when `SentryHelper.CaptureOpencodeError` is called, then zero events are sent to Sentry. *(REQ-001, REQ-003)*
- [ ] **[AC-004]** `SessionService`, `AgentService`, `ProviderService`, and `ProjectService` contain no remaining calls to `SentryHelper.CaptureException` that wrap an `OpencodeApiError` message in a synthetic `InvalidOperationException`. *(REQ-004)*
- [ ] **[AC-005]** The ad-hoc `result.Error.Kind != ErrorKind.NotFound` guards in `SessionService.GetSessionAsync` and `ProjectService.GetCurrentProjectAsync` are removed. *(REQ-005)*
- [ ] **[AC-006]** `CreateSessionForProjectAsync` still re-throws after calling `CaptureOpencodeError`. *(REQ-006)*
- [ ] **[AC-007]** All existing ViewModel-level `catch (Exception ex)` → `SentryHelper.CaptureException` call sites are unchanged. *(REQ-007)*
- [ ] **[AC-008]** Unit tests in `openMob.Tests` verify that `CaptureOpencodeError` does not invoke `SentrySdk` for each expected `ErrorKind`, and does invoke it for `ErrorKind.Unknown`. *(REQ-003)*

---

## Notes for Technical Analysis

> This section is addressed to the agent that will perform the technical implementation analysis.

- **Key areas to investigate:**
  - `SentryHelper.cs` (`src/openMob.Core/Infrastructure/Monitoring/`) — add `CaptureOpencodeError` method. Because `SentryHelper` is `static`, testability requires either extracting an interface (`ISentryHelper`) or using `SentrySdk`'s own test utilities. Evaluate the least-invasive approach that still allows unit testing of AC-008.
  - All four service files listed in REQ-004 — mechanical migration of call sites.
  - `OpencodeApiError` — confirm the `Kind` property is accessible (it is, based on exploration).

- **Suggested implementation approach:**
  - Add to `SentryHelper`:
    ```csharp
    public static void CaptureOpencodeError(
        OpencodeApiError error,
        IDictionary<string, object>? extras = null)
    {
        Debug.WriteLine($"[openMob] Expected API error ({error.Kind}): {error.Message}");
        if (error.Kind != ErrorKind.Unknown)
            return;
        CaptureException(
            new InvalidOperationException($"Unexpected API error: {error.Message}"),
            extras);
    }
    ```
  - In each service, replace:
    ```csharp
    SentryHelper.CaptureException(
        new InvalidOperationException($"Failed to ...: {result.Error.Message}"),
        new Dictionary<string, object> { ["errorKind"] = result.Error.Kind.ToString() });
    ```
    with:
    ```csharp
    SentryHelper.CaptureOpencodeError(result.Error,
        new Dictionary<string, object> { /* context-specific extras */ });
    ```

- **Testability note:** `SentrySdk` in test mode (no DSN configured) silently discards events — it does not throw. To assert that `CaptureException` was or was not called, the test must either: (a) wrap `SentrySdk` behind an interface and mock it, or (b) use Sentry's `SentryOptions.AttachStacktrace` + in-memory transport available in `Sentry.Testing`. Evaluate option (b) first as it avoids introducing a new interface.

- **Constraints to respect:**
  - `openMob.Core` must remain free of MAUI dependencies — `System.Diagnostics.Debug` is acceptable.
  - `SentryHelper` is `static` — do not change this unless testability strictly requires it.
  - Do not modify `ErrorKind`, `OpencodeResult<T>`, or `OpencodeApiError`.
  - All changes must compile with zero warnings (`dotnet build openMob.Core.csproj`).

- **Related files:**
  - `src/openMob.Core/Infrastructure/Monitoring/SentryHelper.cs`
  - `src/openMob.Core/Infrastructure/Http/OpencodeResult.cs` (read-only reference)
  - `src/openMob.Core/Services/SessionService.cs`
  - `src/openMob.Core/Services/AgentService.cs`
  - `src/openMob.Core/Services/ProviderService.cs`
  - `src/openMob.Core/Services/ProjectService.cs`
  - `tests/openMob.Tests/` — new test class required (e.g. `SentryHelperTests.cs`)
