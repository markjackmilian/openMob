# Debug Logging — Runtime Flow & API Tracing

## Metadata
| Field   | Value                        |
|---------|------------------------------|
| Date    | 2026-03-20                   |
| Status  | In Progress                  |
| Version | 1.0                          |

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
   - `LogCommand(string commandName, string phase, string? error = null)` — `phase` is one of: `start`, `complete`, `failed`
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
| `openMob.Core/Infrastructure/DI/CoreServiceExtensions.cs` | Possibly modified | If a write-action delegate needs to be registered |

### Dependencies
- No new NuGet packages expected
- `Android.Util.Log` is available in the `net10.0-android` target of `openMob` — access from Core must be abstracted
- No EF Core migrations required

---

## Open Questions & Clarifications

| # | Question | Status | Answer / Decision |
|---|----------|--------|-------------------|
| 1 | `Android.Util.Log` is part of the Android SDK and cannot be referenced directly from `openMob.Core` (pure .NET library). The write mechanism must be abstracted — options: (a) a static `Action<string, string>` delegate on `DebugLogger` initialised at app startup in `MauiProgram.cs`; (b) a thin `ILogWriter` interface registered via DI. Option (a) is preferred for minimal invasiveness. | **Resolved** | **Option (a) adopted**: `DebugLogger.WriteAction` static delegate, defaulting to no-op. Wired in `MauiProgram.cs` inside `#if DEBUG` and `#if ANDROID` guards. |
| 2 | `INavigationService` implementation location: if it lives in `openMob` (MAUI project), log calls go there; if in `openMob.Core`, they go in Core. Must be confirmed during Technical Analysis. | **Resolved** | `MauiNavigationService` lives in `src/openMob/Services/MauiNavigationService.cs` (MAUI project). Log calls for navigation go in that file. |

---

## Acceptance Criteria

> Each criterion maps to one or more functional requirements.

- [ ] **[AC-001]** Given a DEBUG build, when an HTTP request is made to the opencode server, then a Logcat entry with tag `OM_HTTP` is emitted containing the full URL, method, request body, status code, response body, and duration ms. *(REQ-001, REQ-005)*

- [ ] **[AC-002]** Given a DEBUG build, when an SSE stream is active, then Logcat entries with tag `OM_SSE` are emitted for stream open, each individual chunk (with full content), stream close, and any error. *(REQ-001, REQ-006)*

- [ ] **[AC-003]** Given a DEBUG build, when a ViewModel command is invoked, then Logcat entries with tag `OM_CMD` are emitted at start and at completion (or failure with stack trace). *(REQ-001, REQ-007)*

- [ ] **[AC-004]** Given a DEBUG build, when navigation occurs, then a Logcat entry with tag `OM_NAV` is emitted containing the route and serialised parameters. *(REQ-001, REQ-008)*

- [ ] **[AC-005]** Given a DEBUG build, when a repository operation is executed, then a Logcat entry with tag `OM_DB` is emitted containing operation type, entity name, key info, and duration ms. *(REQ-001, REQ-009)*

- [ ] **[AC-006]** Given a DEBUG build, when a health check, server activation, or mDNS discovery occurs, then a Logcat entry with tag `OM_CONN` is emitted with the relevant details. *(REQ-001, REQ-010)*

- [ ] **[AC-007]** Given a Release build, when any of the above operations occur, then no log entries are emitted and no logging-related allocations occur. *(REQ-012)*

- [ ] **[AC-008]** Given the solution is built targeting `net10.0-android` in DEBUG, the build exits with code 0 and zero warnings. *(REQ-013)*

- [ ] **[AC-009]** `openMob.Core` project builds successfully without any Android SDK or MAUI references. *(REQ-013)*

---

## Notes for Technical Analysis

> This section is addressed to the agent that will perform the technical implementation analysis.

### Key areas to investigate
- Locate all `IOpencodeApiClient` implementation files — identify HTTP methods and SSE/streaming methods that need wrapping.
- Locate all `[RelayCommand]` / `[AsyncRelayCommand]` usages across all ViewModels in `openMob.Core/ViewModels/` — assess volume and best insertion pattern.
- Locate `INavigationService` implementation — determine if it lives in `openMob` or `openMob.Core`, as this affects where log calls are placed.
- Locate all repository implementations in `openMob.Core` — identify public async methods per repository.
- Locate `IOpencodeConnectionManager` and `IOpencodeDiscoveryService` implementations.

### Suggested implementation approach
- **`DebugLogger` write abstraction**: use a static `Action<string, string> WriteAction` property on `DebugLogger`, defaulting to a no-op. In `MauiProgram.cs` (Android target only, inside `#if DEBUG`), assign `DebugLogger.WriteAction = (tag, msg) => Android.Util.Log.Debug(tag, msg)`. This keeps `openMob.Core` free of Android dependencies while requiring only a single line of wiring in the app project.
- **Log serialisation**: use `System.Text.Json.JsonSerializer.Serialize` for structured payload — already available in .NET 10, no extra package needed.
- **SSE chunk counter**: maintain a `[ThreadStatic]` or local counter per stream invocation to track cumulative chunk index.
- **Command timing**: use `Stopwatch.StartNew()` at command start, stop at completion/failure, log `ElapsedMilliseconds`.
- **Database timing**: wrap repository calls with `Stopwatch` at the call site or inside the method body.

### Constraints to respect
- `openMob.Core` must have zero MAUI/Android dependencies (enforced by project structure).
- All logging code must be inside `#if DEBUG` — no `[Conditional("DEBUG")]` attribute alone, as that still compiles the arguments in some cases.
- Minimal invasiveness: prefer adding 2–3 lines per method over introducing new base classes or decorators, unless the volume of affected methods makes a decorator pattern clearly superior.
- As established in the Server Management UI feature, all HTTP operations go through `IOpencodeApiClient` (except `TestConnectionCommand` which uses `IHttpClientFactory` directly) — logging should be centralised in the `IOpencodeApiClient` implementation rather than scattered across call sites.

### Related files or modules (likely — to be confirmed)
- `src/openMob.Core/Infrastructure/` — location for new `Logging/DebugLogger.cs`
- `src/openMob.Core/Services/` or `src/openMob.Core/Api/` — `IOpencodeApiClient` implementation
- `src/openMob.Core/ViewModels/*.cs` — all ViewModel files
- `src/openMob.Core/Data/Repositories/` — repository implementations
- `src/openMob/MauiProgram.cs` — `WriteAction` wiring (Android, DEBUG only)
- `src/openMob.Core/Infrastructure/DI/CoreServiceExtensions.cs` — verify if any DI change is needed

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

> **Note:** `om-mobile-ui` is **not involved** — this feature has no XAML or UI component. Navigation log calls go in `MauiNavigationService.cs` (MAUI project, code-behind only), which is handled by `om-mobile-core` as it is pure C# service logic.

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

### Files to Create

- `src/openMob.Core/Infrastructure/Logging/DebugLogger.cs` — new static class, entire body in `#if DEBUG`

### Files to Modify

| File | Reason |
|------|--------|
| `src/openMob.Core/Infrastructure/Http/OpencodeApiClient.cs` | Add `LogHttp` in `ExecuteAsync<T>` and `LogSse` in `SubscribeToEventsAsync` |
| `src/openMob.Core/ViewModels/SplashViewModel.cs` | Add `LogCommand` to `InitializeAsync` |
| `src/openMob.Core/ViewModels/OnboardingViewModel.cs` | Add `LogCommand` to 5 command methods |
| `src/openMob.Core/ViewModels/ProjectsViewModel.cs` | Add `LogCommand` to 4 command methods |
| `src/openMob.Core/ViewModels/ProjectDetailViewModel.cs` | Add `LogCommand` to 6 command methods |
| `src/openMob.Core/ViewModels/AddProjectViewModel.cs` | Add `LogCommand` to 2 command methods |
| `src/openMob.Core/ViewModels/ChatViewModel.cs` | Add `LogCommand` to 12 command methods |
| `src/openMob.Core/ViewModels/FlyoutViewModel.cs` | Add `LogCommand` to 4 command methods |
| `src/openMob.Core/ViewModels/SettingsViewModel.cs` | Add `LogCommand` to 2 command methods |
| `src/openMob.Core/ViewModels/ServerManagementViewModel.cs` | Add `LogCommand` to 5 command methods |
| `src/openMob.Core/ViewModels/ServerDetailViewModel.cs` | Add `LogCommand` to 4 command methods |
| `src/openMob.Core/ViewModels/ProjectSwitcherViewModel.cs` | Add `LogCommand` to 3 command methods |
| `src/openMob.Core/ViewModels/AgentPickerViewModel.cs` | Add `LogCommand` to 2 command methods |
| `src/openMob.Core/ViewModels/ModelPickerViewModel.cs` | Add `LogCommand` to 3 command methods |
| `src/openMob.Core/ViewModels/ContextSheetViewModel.cs` | Add `LogCommand` to 5 command methods |
| `src/openMob.Core/ViewModels/CommandPaletteViewModel.cs` | Add `LogCommand` to 3 command methods |
| `src/openMob.Core/Data/Repositories/ServerConnectionRepository.cs` | Add `LogDatabase` to all 7 public async methods |
| `src/openMob.Core/Infrastructure/Http/OpencodeConnectionManager.cs` | Add `LogConnection` to `IsServerReachableAsync` and `SetConnectionStatus` |
| `src/openMob.Core/Infrastructure/Discovery/OpencodeDiscoveryService.cs` | Add `LogConnection` (discovery_result) to `ScanAsync` |
| `src/openMob/Services/MauiNavigationService.cs` | Add `LogNavigation` to `GoToAsync` (both overloads) and `PopAsync` |
| `src/openMob/MauiProgram.cs` | Wire `DebugLogger.WriteAction` inside `#if DEBUG && ANDROID` |

---

### Technical Dependencies

- No new NuGet packages required
- No EF Core migrations required
- `Android.Util.Log` accessed only from `openMob` (MAUI project) via the `WriteAction` delegate — never from `openMob.Core`
- `System.Text.Json` already available in .NET 10 — no additional package needed
- `System.Diagnostics.Stopwatch` already available — no additional package needed

---

### Technical Risks

1. **Volume of ViewModel changes (15 files, ~60 command methods):** Each method requires 3–4 lines of `#if DEBUG` instrumentation. Risk of copy-paste errors or missed methods. Mitigation: agent must enumerate all commands from the spec's complete list.
2. **`OpencodeApiClient.ExecuteAsync<T>` is the HTTP chokepoint:** All non-SSE HTTP calls flow through it. Logging here covers all 35+ API methods without touching each individually. Risk: the method signature and retry loop must be understood precisely to place `Stopwatch` correctly.
3. **SSE chunk counter state:** `SubscribeToEventsAsync` is an `async IAsyncEnumerable` — a local `int chunkIndex` variable inside the iterator is sufficient (no `[ThreadStatic]` needed since each call is a single enumeration). Risk: counter must reset per stream invocation, not be static.
4. **`MauiNavigationService` is in the MAUI project:** The `openMob.Core` namespace `using` for `DebugLogger` must be added to this file. Since `openMob` references `openMob.Core`, this is valid.
5. **`#if DEBUG && ANDROID` in MauiProgram.cs:** The `ANDROID` preprocessor symbol is defined by the MAUI SDK for `net10.0-android` targets only. This is the correct guard to prevent iOS build failures.

---

### Execution Order

> Steps that can run in parallel are marked with ⟳. Steps that must be sequential are numbered.

1. **[Git Flow]** Create branch `feature/debug-logging` from `develop`
2. **[om-mobile-core]** Create `DebugLogger.cs` + instrument all call sites (all files listed above)
3. **[om-tester]** Write unit tests for `DebugLogger` (can start once `DebugLogger.cs` is implemented)
4. **[om-reviewer]** Full review against spec
5. **[Fix loop if needed]** Address Critical and Major findings
6. **[Git Flow]** Finish branch and merge into `develop`

> `om-mobile-core` and `om-tester` are **sequential** — tests require the implementation to exist.
> `om-mobile-ui` is **not involved** in this feature.

---

### Definition of Done

- [ ] All `[REQ-001]` through `[REQ-013]` requirements implemented
- [ ] All `[AC-001]` through `[AC-009]` acceptance criteria satisfied
- [ ] Unit tests written for `DebugLogger` covering all 6 log methods
- [ ] `om-reviewer` verdict: ✅ Approved or ⚠️ Approved with remarks
- [ ] `dotnet build src/openMob.Core/openMob.Core.csproj` — zero errors, zero warnings
- [ ] `dotnet build src/openMob/openMob.csproj -f net10.0-android` — zero errors, zero warnings
- [ ] `dotnet test tests/openMob.Tests/openMob.Tests.csproj` — all tests pass
- [ ] Git Flow branch finished and deleted
- [ ] Spec moved to `specs/done/` with Completed status
