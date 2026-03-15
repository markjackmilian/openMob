# opencode API Client

## Metadata
| Field       | Value                                        |
|-------------|----------------------------------------------|
| Date        | 2026-03-15                                   |
| Status      | **Completed**                                |
| Version     | 1.0                                          |
| Completed   | 2026-03-15                                   |
| Branch      | feature/opencode-api-client (merged)         |
| Merged into | develop                                      |

---

## Executive Summary

This spec defines the full HTTP communication layer between openMob and a running opencode server instance. It covers the `IOpencodeApiClient` interface and its implementation, all request/response DTOs derived from the opencode OpenAPI 3.1 specification, SSE event streaming (foreground only), configurable timeout, automatic retry with exponential backoff, and observable waiting-state indicators for the UI. This is the core networking component of the openMob/opencode integration.

**Prerequisite:** spec `2026-03-15-01` (Server Connection Model) must be completed first.

---

## Scope

### In Scope
- `IOpencodeApiClient` interface covering all opencode server API groups:
  - Global (health check)
  - Project (list, current)
  - Path & VCS
  - Config (get, update, providers)
  - Provider (list, auth methods, OAuth)
  - Sessions (full CRUD + fork, share, summarize, revert, permissions, diff, todo, abort)
  - Messages (list, get, send prompt sync, send prompt async, command, shell)
  - Commands (list)
  - Files (find text, find files, find symbols, read, status)
  - Agents (list)
  - Auth (set credentials)
  - Events (SSE stream — foreground only)
  - Logging (write log entry)
- `OpencodeApiClient` sealed implementation using `IHttpClientFactory`
- All request/response DTOs as `sealed record` types, derived from the opencode OpenAPI 3.1 spec
- `IOpencodeConnectionManager` service: resolves the active `ServerConnection`, builds the base URL, injects HTTP Basic Auth header when credentials are present
- Configurable HTTP timeout (user-facing setting, persisted, applied per-request)
- Automatic retry with exponential backoff on connection failure
- `IsWaitingForServer` observable state for UI feedback
- Registration in `AddOpenMobCore()`

### Out of Scope
- TUI control APIs (`/tui/*`) — openMob targets headless server mode only
- Instance dispose API (`POST /instance/dispose`) — too destructive for a mobile client
- Background SSE connection (foreground only in this version)
- HTTPS / custom certificates
- opencode SDK (`@opencode-ai/sdk`) — we implement a native .NET client, not a JS wrapper
- UI components (separate spec)
- mDNS discovery (spec `2026-03-15-03`)

---

## Functional Requirements

> Requirements are numbered for traceability.

### Connection Management

1. **[REQ-001]** The system must define `IOpencodeConnectionManager` with the following responsibilities:
   - Read the active `ServerConnection` from `IServerConnectionRepository`
   - Build the base URL as `http://{host}:{port}`
   - Retrieve the password from `IServerCredentialStore` when `Username` is set
   - Expose `Task<bool> IsServerReachableAsync()` (calls `GET /global/health`)
   - Expose `ServerConnectionStatus ConnectionStatus` as an observable property (values: `Disconnected`, `Connecting`, `Connected`, `Error`)

2. **[REQ-002]** When no active `ServerConnection` exists, all API calls must return a typed error result (not throw) indicating `NoActiveServer`.

3. **[REQ-003]** When HTTP Basic Auth is configured (Username is non-null), the `Authorization: Basic {base64(username:password)}` header must be injected on every request automatically, without requiring callers to handle it.

### HTTP Client

4. **[REQ-004]** `OpencodeApiClient` must be implemented as a `sealed` class using `IHttpClientFactory`. A named `HttpClient` (`"opencode"`) must be registered with the base address resolved at runtime from `IOpencodeConnectionManager`.

5. **[REQ-005]** The HTTP timeout must be configurable by the user via a Settings entry. The timeout value must be persisted (recommend `Preferences` for a single scalar value). The default value is **120 seconds**. The timeout applies to all requests.

6. **[REQ-006]** When a request times out, the client must surface a `OpencodeApiError` with `ErrorKind.Timeout` and a user-readable message. The UI must display this message and offer a manual retry action.

7. **[REQ-007]** All API methods must return `Task<OpencodeResult<T>>` where `OpencodeResult<T>` is a discriminated union type (or equivalent) carrying either a success value or an `OpencodeApiError`. Methods must never throw for expected HTTP errors (4xx, 5xx, timeout, network unreachable).

8. **[REQ-008]** `OpencodeApiError` must carry:
   - `ErrorKind` enum: `Timeout`, `NetworkUnreachable`, `Unauthorized`, `NotFound`, `ServerError`, `NoActiveServer`, `Unknown`
   - `string Message` (user-readable)
   - `int? HttpStatusCode` (null for network-level errors)
   - `Exception? InnerException`

### Retry

9. **[REQ-009]** On `NetworkUnreachable` errors, the client must automatically retry with exponential backoff: delays of 2s, 4s, 8s (max 3 attempts). After the third failure, it must stop retrying and surface the error to the caller.

10. **[REQ-010]** Retry must not apply to: `Unauthorized` (401), `NotFound` (404), or any 4xx client error. Retry applies only to network-level failures and 5xx server errors.

11. **[REQ-011]** During retry, `IOpencodeConnectionManager.ConnectionStatus` must be set to `Connecting`. On final failure, it must be set to `Error`.

### Waiting State

12. **[REQ-012]** `IOpencodeApiClient` must expose `bool IsWaitingForServer` as an observable property (backed by `[ObservableProperty]` in the ViewModel layer or via an event/callback in the service). It must be `true` for the entire duration of any in-flight request and `false` when idle.

13. **[REQ-013]** For long-running operations (specifically `POST /session/:id/message` — synchronous prompt), the waiting state must remain active until the server responds or the timeout is reached. No artificial progress indication is required beyond the boolean flag.

### API Groups

14. **[REQ-014]** **Global** — implement:
    - `GetHealthAsync()` → `OpencodeResult<HealthDto>`

15. **[REQ-015]** **Project** — implement:
    - `GetProjectsAsync()` → `OpencodeResult<IReadOnlyList<ProjectDto>>`
    - `GetCurrentProjectAsync()` → `OpencodeResult<ProjectDto>`

16. **[REQ-016]** **Path & VCS** — implement:
    - `GetPathAsync()` → `OpencodeResult<PathDto>`
    - `GetVcsInfoAsync()` → `OpencodeResult<VcsInfoDto>`

17. **[REQ-017]** **Config** — implement:
    - `GetConfigAsync()` → `OpencodeResult<ConfigDto>`
    - `UpdateConfigAsync(UpdateConfigRequest request)` → `OpencodeResult<ConfigDto>`
    - `GetConfigProvidersAsync()` → `OpencodeResult<ConfigProvidersDto>`

18. **[REQ-018]** **Provider** — implement:
    - `GetProvidersAsync()` → `OpencodeResult<ProvidersDto>`
    - `GetProviderAuthMethodsAsync()` → `OpencodeResult<ProviderAuthMethodsDto>`
    - `AuthorizeProviderOAuthAsync(string providerId)` → `OpencodeResult<ProviderAuthAuthorizationDto>`
    - `HandleProviderOAuthCallbackAsync(string providerId, OAuthCallbackRequest request)` → `OpencodeResult<bool>`

19. **[REQ-019]** **Sessions** — implement:
    - `GetSessionsAsync()` → `OpencodeResult<IReadOnlyList<SessionDto>>`
    - `GetSessionAsync(string id)` → `OpencodeResult<SessionDto>`
    - `GetSessionStatusAsync()` → `OpencodeResult<IReadOnlyDictionary<string, SessionStatusDto>>`
    - `GetSessionChildrenAsync(string id)` → `OpencodeResult<IReadOnlyList<SessionDto>>`
    - `GetSessionTodoAsync(string id)` → `OpencodeResult<IReadOnlyList<TodoDto>>`
    - `CreateSessionAsync(CreateSessionRequest request)` → `OpencodeResult<SessionDto>`
    - `UpdateSessionAsync(string id, UpdateSessionRequest request)` → `OpencodeResult<SessionDto>`
    - `DeleteSessionAsync(string id)` → `OpencodeResult<bool>`
    - `InitSessionAsync(string id, InitSessionRequest request)` → `OpencodeResult<bool>`
    - `ForkSessionAsync(string id, ForkSessionRequest request)` → `OpencodeResult<SessionDto>`
    - `AbortSessionAsync(string id)` → `OpencodeResult<bool>`
    - `ShareSessionAsync(string id)` → `OpencodeResult<SessionDto>`
    - `UnshareSessionAsync(string id)` → `OpencodeResult<SessionDto>`
    - `GetSessionDiffAsync(string id, string? messageId)` → `OpencodeResult<IReadOnlyList<FileDiffDto>>`
    - `SummarizeSessionAsync(string id, SummarizeSessionRequest request)` → `OpencodeResult<bool>`
    - `RevertSessionAsync(string id, RevertSessionRequest request)` → `OpencodeResult<bool>`
    - `UnrevertSessionAsync(string id)` → `OpencodeResult<bool>`
    - `RespondToPermissionAsync(string id, string permissionId, PermissionResponseRequest request)` → `OpencodeResult<bool>`

20. **[REQ-020]** **Messages** — implement:
    - `GetMessagesAsync(string sessionId, int? limit)` → `OpencodeResult<IReadOnlyList<MessageWithPartsDto>>`
    - `GetMessageAsync(string sessionId, string messageId)` → `OpencodeResult<MessageWithPartsDto>`
    - `SendPromptAsync(string sessionId, SendPromptRequest request)` → `OpencodeResult<MessageWithPartsDto>`
    - `SendPromptAsyncNoWait(string sessionId, SendPromptRequest request)` → `OpencodeResult<bool>` (maps to `POST /session/:id/prompt_async`, returns 204)
    - `SendCommandAsync(string sessionId, SendCommandRequest request)` → `OpencodeResult<MessageWithPartsDto>`
    - `RunShellAsync(string sessionId, RunShellRequest request)` → `OpencodeResult<MessageWithPartsDto>`

21. **[REQ-021]** **Commands** — implement:
    - `GetCommandsAsync()` → `OpencodeResult<IReadOnlyList<CommandDto>>`

22. **[REQ-022]** **Files** — implement:
    - `FindTextAsync(string pattern)` → `OpencodeResult<IReadOnlyList<TextMatchDto>>`
    - `FindFilesAsync(FindFilesRequest request)` → `OpencodeResult<IReadOnlyList<string>>`
    - `FindSymbolsAsync(string query)` → `OpencodeResult<IReadOnlyList<SymbolDto>>`
    - `GetFileTreeAsync(string? path)` → `OpencodeResult<IReadOnlyList<FileNodeDto>>`
    - `ReadFileAsync(string path)` → `OpencodeResult<FileContentDto>`
    - `GetFileStatusAsync()` → `OpencodeResult<IReadOnlyList<FileStatusDto>>`

23. **[REQ-023]** **Agents** — implement:
    - `GetAgentsAsync()` → `OpencodeResult<IReadOnlyList<AgentDto>>`

24. **[REQ-024]** **Auth** — implement:
    - `SetProviderAuthAsync(string providerId, SetProviderAuthRequest request)` → `OpencodeResult<bool>`

25. **[REQ-025]** **Logging** — implement:
    - `WriteLogAsync(WriteLogRequest request)` → `OpencodeResult<bool>`

26. **[REQ-026]** **Events (SSE)** — implement:
    - `SubscribeToEventsAsync(CancellationToken cancellationToken)` → `IAsyncEnumerable<OpencodeEventDto>`
    - The implementation must use `HttpClient` with `HttpCompletionOption.ResponseHeadersRead` and stream-parse the SSE protocol (`data:` lines, `event:` lines, `id:` lines).
    - The stream must be cancelled and disposed when the `CancellationToken` is triggered (used by the ViewModel to stop the stream when the app goes to background).
    - The first event received is always `server.connected`; the client must surface it as a typed `OpencodeEventDto` with `EventType = "server.connected"`.

### DTOs

27. **[REQ-027]** All DTOs must be `sealed record` types in `openMob.Core`. They must faithfully represent the shapes defined in the opencode OpenAPI 3.1 spec (available at `http://<host>:<port>/doc`). Property names must use C# PascalCase with `[JsonPropertyName]` attributes mapping to the server's camelCase JSON fields.

28. **[REQ-028]** `OpencodeEventDto` must carry:
    - `string EventType`
    - `string? EventId`
    - `JsonElement? Data` (raw JSON for flexibility, since event payloads vary by type)

### Settings

29. **[REQ-029]** A `IOpencodeSettingsService` interface must be defined with:
    - `int GetTimeoutSeconds()` / `Task SetTimeoutSecondsAsync(int value)`
    - Default: `120`
    - Backed by `Microsoft.Maui.Storage.Preferences` (via an abstraction to keep Core testable)

---

## Functional Impacts

### Affected Components / Systems
| Component | Impact | Notes |
|-----------|--------|-------|
| `CoreServiceExtensions` | Modified | Register `IOpencodeApiClient`, `IOpencodeConnectionManager`, `IOpencodeSettingsService` |
| `AppDbContext` | None | No new entities in this spec |
| `IClaudeApiClient` | None | Existing client unaffected; patterns are parallel |
| `SecureStorage` | Read only | Credentials read via `IServerCredentialStore` (from spec 01) |
| `Preferences` | New usage | Timeout setting stored as a scalar preference |
| `MauiProgram.cs` | Modified | Register MAUI-side implementations of any new interfaces requiring platform APIs |

### Dependencies
- **Depends on:** spec `2026-03-15-01` — `IServerConnectionRepository` and `IServerCredentialStore` must exist
- **Required by:** all ViewModels that interact with the opencode server

---

## Open Questions & Clarifications

| # | Question | Status | Answer / Decision |
|---|----------|--------|-------------------|
| 1 | Should the timeout apply uniformly to all requests, or only to long-running ones (`SendPromptAsync`)? | **Decided** | Uniform for simplicity; a per-call override can be added later |
| 2 | Should retry count (max 3) be hardcoded or also user-configurable? | **Decided** | Hardcoded for v1; expose via `IOpencodeSettingsService` in a future iteration |
| 3 | `OpencodeResult<T>` — use a custom discriminated union, `OneOf<T, OpencodeApiError>`, or `FluentResults`? | **Decided** | Lightweight custom `readonly struct OpencodeResult<T>` — no third-party dependencies |
| 4 | Should `IOpencodeConnectionManager.ConnectionStatus` be an `IObservable<T>` (Rx) or a simple event + polling? | **Decided** | Simple `event Action<ServerConnectionStatus> StatusChanged` — no Rx dependency |

---

## Acceptance Criteria

> Each criterion maps to one or more functional requirements.

- [ ] **[AC-001]** Given an active server connection with no password, when any API method is called, then the request is sent without an `Authorization` header. *(REQ-003)*
- [ ] **[AC-002]** Given an active server connection with username and password, when any API method is called, then the request includes a valid `Authorization: Basic` header. *(REQ-003)*
- [ ] **[AC-003]** Given no active server connection, when any API method is called, then the result carries `ErrorKind.NoActiveServer` and no HTTP request is made. *(REQ-002)*
- [ ] **[AC-004]** Given a request that exceeds the configured timeout, when the timeout fires, then the result carries `ErrorKind.Timeout` and `IsWaitingForServer` returns to `false`. *(REQ-005, REQ-006, REQ-012)*
- [ ] **[AC-005]** Given a server that is unreachable, when a request is made, then the client retries 3 times with exponential backoff before returning `ErrorKind.NetworkUnreachable`. *(REQ-009)*
- [ ] **[AC-006]** Given a 401 response, when the client receives it, then no retry is attempted and `ErrorKind.Unauthorized` is returned immediately. *(REQ-010)*
- [ ] **[AC-007]** Given an active SSE subscription, when the `CancellationToken` is cancelled, then the HTTP stream is closed and the `IAsyncEnumerable` completes without exception. *(REQ-026)*
- [ ] **[AC-008]** Given a running opencode server, when `GetHealthAsync()` is called, then the result contains `Healthy = true` and a non-empty `Version` string. *(REQ-014)*
- [ ] **[AC-009]** Given a running opencode server, when `SendPromptAsync` is called, then `IsWaitingForServer` is `true` for the duration of the call and `false` after. *(REQ-012, REQ-013)*
- [ ] **[AC-010]** All DTO properties deserialize correctly from the opencode server's JSON responses (verified against the live OpenAPI spec at `/doc`). *(REQ-027)*
- [ ] **[AC-011]** Build and all tests pass with zero warnings. *(all)*

---

## Notes for Technical Analysis

> This section is addressed to the agent that will perform the technical implementation analysis.

- **Existing pattern to follow:** `IClaudeApiClient` / `ClaudeApiClient` at `src/openMob.Core/Infrastructure/Http/`. The new `IOpencodeApiClient` / `OpencodeApiClient` must follow the same conventions (file-scoped namespace, sealed, `IHttpClientFactory`, constructor injection).
- **Named HttpClient registration:** Use `services.AddHttpClient("opencode")` in `CoreServiceExtensions`. The base address cannot be set at registration time (it is dynamic, resolved from the active `ServerConnection`); instead, `OpencodeApiClient` must call `_httpClientFactory.CreateClient("opencode")` and set `BaseAddress` per-call, or use `HttpRequestMessage` with absolute URIs.
- **SSE parsing:** .NET 10 does not include a built-in SSE client for `HttpClient`. Implement a lightweight line-by-line parser over `StreamReader` reading from `response.Content.ReadAsStreamAsync()`. Do not introduce a third-party SSE library unless the technical analysis determines it is necessary.
- **`OpencodeResult<T>` pattern:** Implement as a `readonly struct` with implicit conversions from `T` and `OpencodeApiError` to avoid boxing. Example:
  ```csharp
  readonly struct OpencodeResult<T> {
      public bool IsSuccess { get; }
      public T? Value { get; }
      public OpencodeApiError? Error { get; }
  }
  ```
- **Timeout implementation:** Set `HttpClient.Timeout` from `IOpencodeSettingsService.GetTimeoutSeconds()` before each request (or create a new client per request via factory). Catch `TaskCanceledException` with `ex.InnerException is TimeoutException` to distinguish timeout from user cancellation.
- **`IOpencodeSettingsService` MAUI implementation:** `MauiOpencodeSettingsService` in `src/openMob/Infrastructure/Settings/` using `Microsoft.Maui.Storage.Preferences`. Register in `MauiProgram.cs`.
- **DTO source of truth:** The opencode OpenAPI 3.1 spec is live at `http://localhost:4096/doc` when a server is running. The TypeScript types are also available at `https://github.com/anomalyco/opencode/blob/dev/packages/sdk/js/src/gen/types.gen.ts` as a reference for field names and shapes.
- **File locations:**
  - Interface: `src/openMob.Core/Infrastructure/Http/IOpencodeApiClient.cs`
  - Implementation: `src/openMob.Core/Infrastructure/Http/OpencodeApiClient.cs`
  - DTOs: `src/openMob.Core/Infrastructure/Http/Dtos/Opencode/`
  - Connection manager: `src/openMob.Core/Infrastructure/Http/IOpencodeConnectionManager.cs`
  - Settings service: `src/openMob.Core/Infrastructure/Settings/IOpencodeSettingsService.cs`
  - MAUI settings impl: `src/openMob/Infrastructure/Settings/MauiOpencodeSettingsService.cs`
- **As established in spec `2026-03-13-project-scaffolding`**, the project uses `IHttpClientFactory` (not typed `HttpClient` bindings) and all injectable services must have a backing interface. This spec must not deviate from that decision.

---

## Technical Analysis

> Added by: om-orchestrator | Date: 2026-03-15

### Change Classification

| Field | Value |
|-------|-------|
| Change type | Feature |
| Git Flow branch | `feature/opencode-api-client` |
| Branches from | `develop` |
| Estimated complexity | High |
| Estimated agents involved | om-mobile-core, om-tester, om-reviewer |

### Layers Involved

| Layer | Agent | Scope |
|-------|-------|-------|
| HTTP Client / Services | om-mobile-core | `src/openMob.Core/Infrastructure/Http/` |
| DTOs | om-mobile-core | `src/openMob.Core/Infrastructure/Http/Dtos/Opencode/` |
| Connection Manager | om-mobile-core | `src/openMob.Core/Infrastructure/Http/` |
| Settings Service (interface) | om-mobile-core | `src/openMob.Core/Infrastructure/Settings/` |
| Settings Service (MAUI impl) | om-mobile-core | `src/openMob/Infrastructure/Settings/` |
| DI Registration | om-mobile-core | `src/openMob.Core/Infrastructure/DI/CoreServiceExtensions.cs`, `src/openMob/MauiProgram.cs` |
| Unit Tests | om-tester | `tests/openMob.Tests/` |
| Code Review | om-reviewer | all of the above |

### Files to Create

**openMob.Core (pure .NET — no MAUI deps):**
- `src/openMob.Core/Infrastructure/Http/IOpencodeApiClient.cs` — full interface with all 40+ API methods
- `src/openMob.Core/Infrastructure/Http/OpencodeApiClient.cs` — sealed implementation
- `src/openMob.Core/Infrastructure/Http/IOpencodeConnectionManager.cs` — interface + `ServerConnectionStatus` enum
- `src/openMob.Core/Infrastructure/Http/OpencodeConnectionManager.cs` — implementation
- `src/openMob.Core/Infrastructure/Http/OpencodeResult.cs` — `readonly struct OpencodeResult<T>` + `OpencodeApiError` + `ErrorKind`
- `src/openMob.Core/Infrastructure/Settings/IOpencodeSettingsService.cs` — interface
- `src/openMob.Core/Infrastructure/Http/Dtos/Opencode/HealthDto.cs`
- `src/openMob.Core/Infrastructure/Http/Dtos/Opencode/ProjectDto.cs`
- `src/openMob.Core/Infrastructure/Http/Dtos/Opencode/PathDto.cs`
- `src/openMob.Core/Infrastructure/Http/Dtos/Opencode/VcsInfoDto.cs`
- `src/openMob.Core/Infrastructure/Http/Dtos/Opencode/ConfigDto.cs`
- `src/openMob.Core/Infrastructure/Http/Dtos/Opencode/ProviderDto.cs` — `ProvidersDto`, `ProviderAuthMethodsDto`, `ProviderAuthAuthorizationDto`
- `src/openMob.Core/Infrastructure/Http/Dtos/Opencode/SessionDto.cs` — `SessionDto`, `SessionStatusDto`, `FileDiffDto`, `TodoDto`
- `src/openMob.Core/Infrastructure/Http/Dtos/Opencode/MessageDto.cs` — `MessageWithPartsDto`, `PartDto` (discriminated union via `JsonElement` for part payload)
- `src/openMob.Core/Infrastructure/Http/Dtos/Opencode/CommandDto.cs`
- `src/openMob.Core/Infrastructure/Http/Dtos/Opencode/FileDto.cs` — `TextMatchDto`, `SymbolDto`, `FileNodeDto`, `FileContentDto`, `FileStatusDto`
- `src/openMob.Core/Infrastructure/Http/Dtos/Opencode/AgentDto.cs`
- `src/openMob.Core/Infrastructure/Http/Dtos/Opencode/OpencodeEventDto.cs`
- `src/openMob.Core/Infrastructure/Http/Dtos/Opencode/Requests/` — all request record types (CreateSessionRequest, UpdateSessionRequest, ForkSessionRequest, SummarizeSessionRequest, RevertSessionRequest, PermissionResponseRequest, InitSessionRequest, SendPromptRequest, SendCommandRequest, RunShellRequest, UpdateConfigRequest, OAuthCallbackRequest, FindFilesRequest, SetProviderAuthRequest, WriteLogRequest)

**openMob MAUI project:**
- `src/openMob/Infrastructure/Settings/MauiOpencodeSettingsService.cs` — `Preferences`-backed implementation

**Tests:**
- `tests/openMob.Tests/Infrastructure/Http/OpencodeApiClientTests.cs`
- `tests/openMob.Tests/Infrastructure/Http/OpencodeConnectionManagerTests.cs`
- `tests/openMob.Tests/Infrastructure/Http/OpencodeResultTests.cs`

### Files to Modify

- `src/openMob.Core/Infrastructure/DI/CoreServiceExtensions.cs` — register `IOpencodeApiClient`, `IOpencodeConnectionManager`; add `services.AddHttpClient("opencode")`
- `src/openMob/MauiProgram.cs` — register `MauiOpencodeSettingsService` as `IOpencodeSettingsService`

### Technical Dependencies

- **Prerequisite:** `IServerConnectionRepository` and `IServerCredentialStore` from spec `2026-03-15-01` — ✅ already merged into `develop`
- **opencode API endpoints used:** all groups listed in REQ-014 through REQ-026
- **New NuGet packages:** none required — SSE parsing is implemented manually; `System.Text.Json` is already available via .NET 10

### Technical Risks

1. **`HttpClient.Timeout` vs `CancellationToken` ambiguity:** `TaskCanceledException` is thrown for both user cancellation and timeout. Must check `ex.InnerException is TimeoutException` (or `cancellationToken.IsCancellationRequested`) to distinguish the two cases correctly.
2. **SSE stream lifetime:** The `IAsyncEnumerable` must properly dispose the `HttpResponseMessage` and `StreamReader` when the `CancellationToken` is cancelled. Use `await using` / `try/finally` to guarantee disposal.
3. **Dynamic base URL:** `HttpClient` base address cannot be set at DI registration time. `OpencodeApiClient` must build absolute `Uri` objects per-request from `IOpencodeConnectionManager`. This means `HttpClient.Timeout` must be set on the client instance returned by `_httpClientFactory.CreateClient("opencode")` before each call — or use a `CancellationTokenSource` with `CancelAfter` instead of relying on `HttpClient.Timeout`.
4. **DTO complexity:** The opencode `Part` type is a discriminated union of 11 subtypes. Use `JsonElement` for the part payload in `MessageWithPartsDto` to avoid a complex polymorphic deserializer. Typed part access can be added in a future spec.
5. **`ConfigDto` size:** The `Config` type from the OpenAPI spec is very large (keybinds, agents, providers, MCP, etc.). Implement it faithfully but mark non-critical nested objects as `JsonElement?` where the shape is too complex for v1.
6. **`IsWaitingForServer` thread safety:** The property is set from async HTTP calls; must use `Interlocked` or ensure updates happen on the main thread if the service is consumed directly by a ViewModel.

### Execution Order

> Steps that can run in parallel are marked with ⟳. Steps that must be sequential are numbered.

1. **[Git Flow]** Create branch `feature/opencode-api-client` from `develop`
2. **[om-mobile-core]** Implement `OpencodeResult<T>`, `OpencodeApiError`, `ErrorKind` (foundational types — no dependencies)
3. **[om-mobile-core]** Implement `IOpencodeSettingsService` interface + `MauiOpencodeSettingsService`
4. **[om-mobile-core]** Implement `IOpencodeConnectionManager` + `OpencodeConnectionManager`
5. **[om-mobile-core]** Implement all DTOs and request records in `Dtos/Opencode/`
6. **[om-mobile-core]** Implement `IOpencodeApiClient` interface + `OpencodeApiClient` (all API groups + SSE)
7. **[om-mobile-core]** Register all services in `CoreServiceExtensions` and `MauiProgram.cs`
8. **[om-tester]** Write unit tests for `OpencodeConnectionManager`, `OpencodeApiClient` (mocked `HttpMessageHandler`), `OpencodeResult`
9. **[om-reviewer]** Full review against spec
10. **[Fix loop if needed]** Address Critical and Major findings
11. **[Git Flow]** Finish branch and merge into `develop`

### Definition of Done

- [ ] All `[REQ-001]` through `[REQ-029]` requirements implemented
- [ ] All `[AC-001]` through `[AC-011]` acceptance criteria satisfied
- [ ] Unit tests written for `OpencodeConnectionManager`, `OpencodeApiClient`, `OpencodeResult`
- [ ] `om-reviewer` verdict: ✅ Approved or ⚠️ Approved with remarks
- [ ] Git Flow branch finished and deleted
- [ ] Spec moved to `specs/done/` with Completed status
