# Debug Logging — Runtime Flow & API Tracing

## Metadata
| Field       | Value                              |
|-------------|------------------------------------|
| Date        | 2026-03-20                         |
| Status      | **Completed**                      |
| Version     | 1.0                                |
| Completed   | 2026-03-21                         |
| Branch      | feature/debug-logging (merged)     |
| Merged into | develop                            |

---

## Executive Summary

This feature introduces a structured debug logging infrastructure for the openMob app, active exclusively in `#if DEBUG` builds. Logs are written to Android Logcat with structured fields (timestamp, thread ID, tag, method, payload) and cover all critical high-traffic zones: HTTP calls, SSE streaming, ViewModel commands, navigation, database operations, and server connection management. The goal is to enable runtime flow analysis, behaviour verification, and debugging without any impact on Release builds.

---

## Scope

### In Scope
- A static `DebugLogger` class in `openMob.Core.Infrastructure.Logging`, fully wrapped in `#if DEBUG`
- Structured log entries written to Android Logcat via `Android.Util.Log.Debug(tag, message)`
- Coverage of all critical layers:
  - **HTTP** — full request/response logging (URL, method, headers, body, status code, duration ms)
  - **SSE/Streaming** — stream open, every chunk (full content), stream close, exceptions
  - **ViewModel Commands** — command name, start, completion outcome, exception stack trace
  - **Navigation** — route, parameters on every `GoToAsync` / `PopAsync` call
  - **Database** — operation type (Get/Add/Update/Delete), entity name, key parameters, duration ms
  - **Server Connection** — health check (URL, outcome, duration), active server change (ID, name), mDNS discovery results
- Logcat tags prefixed with `OM_` per layer: `OM_HTTP`, `OM_SSE`, `OM_CMD`, `OM_NAV`, `OM_DB`, `OM_CONN`
- Minimal invasiveness: log calls added at existing entry/exit points only, no structural refactoring
- `DebugLogger` is a pure static class — no DI, no constructor injection, usable anywhere

### Out of Scope
- Any logging in Release builds
- Sentry integration or file-based logging
- In-app log viewer UI
- MAUI lifecycle event logging (OnAppearing, OnDisappearing, etc.)
- iOS logging (Logcat is Android-only; iOS support deferred)
- Correlation IDs (thread ID + timestamp are sufficient)
- Log sanitisation or masking of sensitive data (in DEBUG, everything is logged as-is)

---

## Functional Requirements

> Requirements are numbered for traceability.

1. **[REQ-001]** A static class `DebugLogger` must be created in `openMob.Core/Infrastructure/Logging/DebugLogger.cs`, with all its content enclosed in `#if DEBUG` / `#endif` preprocessor directives.

2. **[REQ-002]** `DebugLogger` must expose the following static methods, each dedicated to a specific layer:
   - `LogHttp(string method, string url, string? requestBody, int statusCode, string? responseBody, long durationMs)`
   - `LogSse(string eventType, string? chunk)` — `eventType` is one of: `open`, `chunk`, `close`, `error`
   - `LogCommand(string commandName, string phase, long durationMs = 0, string? error = null)` — `phase` is one of: `start`, `complete`, `failed`
   - `LogNavigation(string route, object? parameters = null)`
   - `LogDatabase(string operation, string entity, string? keyInfo, long durationMs)`
   - `LogConnection(string eventType, string? detail = null, long durationMs = 0)` — `eventType` is one of: `health_check`, `server_changed`, `discovery_result`

3. **[REQ-003]** Every log entry must include the following structured fields serialised as a single-line JSON string:
   - `ts` — ISO 8601 timestamp with milliseconds (e.g. `2026-03-20T14:32:01.123Z`)
   - `tid` — managed thread ID (`Environment.CurrentManagedThreadId`)
   - `layer` — layer name (e.g. `HTTP`, `SSE`, `CMD`, `NAV`, `DB`, `CONN`)
   - `method` or `event` — the specific operation name
   - Additional payload fields specific to each layer (see REQ-005 through REQ-010)

4. **[REQ-004]** Log output must be written using `Android.Util.Log.Debug(tag, message)` where `tag` is the layer-specific `OM_*` prefix. The call to `Android.Util.Log` must be isolated so that `openMob.Core` does not take a direct dependency on the Android SDK — the write action must be injected or abstracted (see Notes for Technical Analysis).

5. **[REQ-005]** **HTTP layer** — `LogHttp` must log:
   - Request: HTTP method, full URL, request body (full content)
   - Response: HTTP status code, response body (full content), duration in ms
   - Tag: `OM_HTTP`

6. **[REQ-006]** **SSE/Streaming layer** — `LogSse` must log:
   - `open`: stream URL and timestamp
   - `chunk`: full chunk content and cumulative chunk index
   - `close`: total chunks received, total duration ms
   - `error`: exception type and message
   - Tag: `OM_SSE`

7. **[REQ-007]** **ViewModel Commands layer** — `LogCommand` must log:
   - `start`: command name, thread ID, timestamp
   - `complete`: command name, duration ms
   - `failed`: command name, exception type, full exception message and stack trace
   - Tag: `OM_CMD`

8. **[REQ-008]** **Navigation layer** — `LogNavigation` must log:
   - Route string (e.g. `server-detail`, `..`)
   - Parameters serialised as JSON (if any)
   - Tag: `OM_NAV`

9. **[REQ-009]** **Database layer** — `LogDatabase` must log:
   - Operation type: `Get`, `GetAll`, `Add`, `Update`, `Delete`, `SetActive`
   - Entity name (e.g. `ServerConnection`, `Session`, `Message`)
   - Key info (e.g. entity ID or filter description)
   - Duration ms
   - Tag: `OM_DB`

10. **[REQ-010]** **Server Connection layer** — `LogConnection` must log:
    - `health_check`: URL probed, HTTP status or exception, duration ms
    - `server_changed`: server ID and name of the newly active server
    - `discovery_result`: number of servers discovered, list of host:port entries
    - Tag: `OM_CONN`

11. **[REQ-011]** `DebugLogger` calls must be added at the entry and exit points of the following existing components (non-exhaustive — Technical Analysis must identify exact files):
    - `IOpencodeApiClient` implementation — HTTP and SSE methods
    - All `[RelayCommand]` / `[AsyncRelayCommand]` methods in all ViewModels
    - `INavigationService` implementation — `GoToAsync`, `PopAsync`
    - All repository implementations — each public async method
    - `IOpencodeConnectionManager` implementation — health check, server activation
    - `IOpencodeDiscoveryService` implementation — `ScanAsync` results

12. **[REQ-012]** `DebugLogger` must have zero runtime overhead in Release builds. All logging code must be excluded at compile time via `#if DEBUG`. No logging-related allocations must occur in Release.

13. **[REQ-013]** `DebugLogger` must not introduce MAUI or Android SDK dependencies into `openMob.Core`. The mechanism for writing to Logcat must be decoupled from the Core library (see Notes for Technical Analysis).

---

## Functional Impacts

### Affected Components / Systems
| Component | Impact | Notes |
|-----------|--------|-------|
| `openMob.Core/Infrastructure/Logging/DebugLogger.cs` | New file | Pure static class, `#if DEBUG` only |
| `IOpencodeApiClient` implementation | Modified | Add `LogHttp` and `LogSse` calls |
| All ViewModel classes | Modified | Add `LogCommand` calls in `[RelayCommand]` methods |
| `INavigationService` implementation | Modified | Add `LogNavigation` calls |
| All repository implementations | Modified | Add `LogDatabase` calls |
| `IOpencodeConnectionManager` implementation | Modified | Add `LogConnection` calls |
| `IOpencodeDiscoveryService` implementation | Modified | Add `LogConnection` (discovery_result) calls |
| `openMob.Core/Infrastructure/DI/CoreServiceExtensions.cs` | Not modified | No DI changes needed — WriteAction delegate used instead |

### Dependencies
- No new NuGet packages
- `Android.Util.Log` accessed only from `openMob` (MAUI project) via the `WriteAction` delegate
- No EF Core migrations required

---

## Open Questions & Clarifications

| # | Question | Status | Answer / Decision |
|---|----------|--------|-------------------|
| 1 | `Android.Util.Log` abstraction approach | **Resolved** | **Option (a) adopted**: `DebugLogger.WriteAction` static delegate, defaulting to no-op. Wired in `MauiProgram.cs` inside `#if DEBUG && ANDROID` guards. |
| 2 | `INavigationService` implementation location | **Resolved** | `MauiNavigationService` lives in `src/openMob/Services/MauiNavigationService.cs` (MAUI project). Log calls for navigation go in that file. |

---

## Acceptance Criteria

> Each criterion maps to one or more functional requirements.

- [x] **[AC-001]** Given a DEBUG build, when an HTTP request is made to the opencode server, then a Logcat entry with tag `OM_HTTP` is emitted containing the full URL, method, request body, status code, response body, and duration ms. *(REQ-001, REQ-005)*

- [x] **[AC-002]** Given a DEBUG build, when an SSE stream is active, then Logcat entries with tag `OM_SSE` are emitted for stream open, each individual chunk (with full content), stream close, and any error. *(REQ-001, REQ-006)*

- [x] **[AC-003]** Given a DEBUG build, when a ViewModel command is invoked, then Logcat entries with tag `OM_CMD` are emitted at start and at completion (or failure with stack trace). *(REQ-001, REQ-007)*

- [x] **[AC-004]** Given a DEBUG build, when navigation occurs, then a Logcat entry with tag `OM_NAV` is emitted containing the route and serialised parameters. *(REQ-001, REQ-008)*

- [x] **[AC-005]** Given a DEBUG build, when a repository operation is executed, then a Logcat entry with tag `OM_DB` is emitted containing operation type, entity name, key info, and duration ms. *(REQ-001, REQ-009)*

- [x] **[AC-006]** Given a DEBUG build, when a health check, server activation, or mDNS discovery occurs, then a Logcat entry with tag `OM_CONN` is emitted with the relevant details. *(REQ-001, REQ-010)*

- [x] **[AC-007]** Given a Release build, when any of the above operations occur, then no log entries are emitted and no logging-related allocations occur. *(REQ-012)*

- [x] **[AC-008]** Given the solution is built targeting `net10.0-android` in DEBUG, the build exits with code 0 and zero warnings. *(REQ-013)*

- [x] **[AC-009]** `openMob.Core` project builds successfully without any Android SDK or MAUI references. *(REQ-013)*

---

## Technical Analysis

> Added by: om-orchestrator | Date: 2026-03-20

### Open Questions — Resolved

**Q1 — Android write abstraction:** Confirmed **Option (a)**: a static `Action<string, string> WriteAction` delegate on `DebugLogger`, defaulting to `(_, _) => { }` (no-op). Wired in `MauiProgram.cs` inside `#if DEBUG` + `#if ANDROID` guards:
```csharp
#if DEBUG && ANDROID
DebugLogger.WriteAction = (tag, msg) => Android.Util.Log.Debug(tag, msg);
#endif
```
This requires zero DI changes and zero new interfaces. `CoreServiceExtensions.cs` is **not modified**.

**Q2 — INavigationService location:** Confirmed. `MauiNavigationService` lives in `src/openMob/Services/MauiNavigationService.cs` (MAUI project). Navigation log calls go in that file, not in Core.

---

### Change Classification

| Field | Value |
|-------|-------|
| Change type | Feature |
| Git Flow branch | `feature/debug-logging` |
| Branches from | `develop` |
| Estimated complexity | Medium |
| Estimated agents involved | om-mobile-core, om-tester, om-reviewer |

---

### Layers Involved

| Layer | Agent | Scope |
|-------|-------|-------|
| New `DebugLogger` static class | om-mobile-core | `src/openMob.Core/Infrastructure/Logging/DebugLogger.cs` |
| HTTP + SSE instrumentation | om-mobile-core | `src/openMob.Core/Infrastructure/Http/OpencodeApiClient.cs` |
| ViewModel command instrumentation | om-mobile-core | `src/openMob.Core/ViewModels/*.cs` (15 files) |
| Navigation instrumentation | om-mobile-core | `src/openMob/Services/MauiNavigationService.cs` |
| Repository instrumentation | om-mobile-core | `src/openMob.Core/Data/Repositories/ServerConnectionRepository.cs` |
| Connection manager instrumentation | om-mobile-core | `src/openMob.Core/Infrastructure/Http/OpencodeConnectionManager.cs` |
| Discovery service instrumentation | om-mobile-core | `src/openMob.Core/Infrastructure/Discovery/OpencodeDiscoveryService.cs` |
| MauiProgram WriteAction wiring | om-mobile-core | `src/openMob/MauiProgram.cs` |
| Unit Tests | om-tester | `tests/openMob.Tests/Infrastructure/Logging/DebugLoggerTests.cs` |
| Code Review | om-reviewer | All of the above |

---

### Files Created

- `src/openMob.Core/Infrastructure/Logging/DebugLogger.cs` — new static class, entire body in `#if DEBUG`
- `tests/openMob.Tests/Infrastructure/Logging/DebugLoggerTests.cs` — 18 unit tests

### Files Modified

| File | Reason |
|------|--------|
| `src/openMob.Core/Infrastructure/Http/OpencodeApiClient.cs` | `LogHttp` in `ExecuteAsync<T>`, `LogSse` in `SubscribeToEventsAsync` (SSE state as local vars) |
| `src/openMob.Core/ViewModels/SplashViewModel.cs` | `LogCommand` on `InitializeAsync` |
| `src/openMob.Core/ViewModels/OnboardingViewModel.cs` | `LogCommand` on 5 command methods |
| `src/openMob.Core/ViewModels/ProjectsViewModel.cs` | `LogCommand` on 4 command methods |
| `src/openMob.Core/ViewModels/ProjectDetailViewModel.cs` | `LogCommand` on 6 command methods |
| `src/openMob.Core/ViewModels/AddProjectViewModel.cs` | `LogCommand` on 2 command methods |
| `src/openMob.Core/ViewModels/ChatViewModel.cs` | `LogCommand` on 12 command methods |
| `src/openMob.Core/ViewModels/FlyoutViewModel.cs` | `LogCommand` on 4 command methods |
| `src/openMob.Core/ViewModels/SettingsViewModel.cs` | `LogCommand` on 2 command methods |
| `src/openMob.Core/ViewModels/ServerManagementViewModel.cs` | `LogCommand` on 5 command methods |
| `src/openMob.Core/ViewModels/ServerDetailViewModel.cs` | `LogCommand` on 4 command methods |
| `src/openMob.Core/ViewModels/ProjectSwitcherViewModel.cs` | `LogCommand` on 3 command methods |
| `src/openMob.Core/ViewModels/AgentPickerViewModel.cs` | `LogCommand` on 2 command methods |
| `src/openMob.Core/ViewModels/ModelPickerViewModel.cs` | `LogCommand` on 3 command methods |
| `src/openMob.Core/ViewModels/ContextSheetViewModel.cs` | `LogCommand` on 5 command methods |
| `src/openMob.Core/ViewModels/CommandPaletteViewModel.cs` | `LogCommand` on 3 command methods |
| `src/openMob.Core/Data/Repositories/ServerConnectionRepository.cs` | `LogDatabase` on all 7 public async methods; `LogConnection("server_changed")` in `SetActiveAsync` |
| `src/openMob.Core/Infrastructure/Http/OpencodeConnectionManager.cs` | `LogConnection("health_check")` in `IsServerReachableAsync` |
| `src/openMob.Core/Infrastructure/Discovery/OpencodeDiscoveryService.cs` | `LogConnection("discovery_result")` per discovered server in `ScanAsync` |
| `src/openMob/Services/MauiNavigationService.cs` | `LogNavigation` on `GoToAsync` (both overloads) and `PopAsync` |
| `src/openMob/MauiProgram.cs` | `DebugLogger.WriteAction` wired inside `#if DEBUG && ANDROID` |

---

### Key Implementation Decisions

1. **`WriteAction` delegate pattern** — `DebugLogger.WriteAction` static delegate defaults to no-op. Wired to `Android.Util.Log.Debug` only in `MauiProgram.cs` under `#if DEBUG && ANDROID`. Zero DI changes, zero new interfaces.

2. **SSE state as local variables** — `sseChunkIndex` and `sseStreamStartMs` are local variables inside `OpencodeApiClient.SubscribeToEventsAsync`, not static fields. Eliminates race condition if two SSE streams run concurrently.

3. **`LogCommand` signature** — `(string commandName, string phase, long durationMs = 0, string? error = null)`. `duration_ms` is a proper `long` JSON number, not a string. Sync commands pass `durationMs = 0`.

4. **`server_changed` log placement** — `LogConnection("server_changed", $"id={id} name={entity.Name}")` placed in `ServerConnectionRepository.SetActiveAsync` (where entity identity is known), not in `OpencodeConnectionManager.SetConnectionStatus`.

5. **`LogSse("open")` includes URL** — stream URL passed as the `chunk` parameter for the `"open"` event, serialised as a `url` JSON field.

---

### Review Outcome

| Round | Verdict | Critical | Major | Minor |
|-------|---------|----------|-------|-------|
| Round 1 | 🔴 Changes required | 0 | 3 | 6 |
| Round 2 | ⚠️ Approved with remarks | 0 | 0 | 1 |
| Round 2 minor fix | ✅ Resolved | — | — | — |

**Final verdict: ⚠️ Approved with remarks** (zero Critical, zero Major)

---

### Definition of Done

- [x] All `[REQ-001]` through `[REQ-013]` requirements implemented
- [x] All `[AC-001]` through `[AC-009]` acceptance criteria satisfied
- [x] 18 unit tests written for `DebugLogger` covering all 6 log methods
- [x] `om-reviewer` verdict: ⚠️ Approved with remarks
- [x] `dotnet build src/openMob.Core/openMob.Core.csproj` — zero errors, zero warnings
- [x] `dotnet build src/openMob/openMob.csproj -f net10.0-android` — zero errors, zero warnings
- [x] `dotnet test tests/openMob.Tests/openMob.Tests.csproj` — all 18 new tests pass
- [x] Git Flow branch finished and deleted
- [x] Spec moved to `specs/done/` with Completed status
