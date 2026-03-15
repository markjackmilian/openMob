# opencode mDNS Discovery

## Metadata
| Field   | Value                        |
|---------|------------------------------|
| Date    | 2026-03-15                   |
| Status  | In Progress                  |
| Version | 1.0                          |

---

## Executive Summary

This spec defines the mDNS (Multicast DNS / Zeroconf) discovery service that allows openMob to automatically detect opencode server instances running on the local network. When the user initiates a scan, the app queries the local network for services advertised under the default opencode mDNS domain, presents a list of discovered instances, and allows the user to select one to save as a `ServerConnection`. This feature is independent of the HTTP client layer and can be developed in parallel with spec `2026-03-15-02`.

**Prerequisite:** spec `2026-03-15-01` (Server Connection Model) must be completed first — discovered servers are persisted as `ServerConnection` records.

---

## Scope

### In Scope
- `IOpencodeDiscoveryService` interface for mDNS scanning
- Discovery of opencode instances advertising on `_opencode._tcp.local` (or the equivalent mDNS service type used by opencode)
- Return of a list of discovered `DiscoveredServerDto` records (name, host, port)
- Scan lifecycle: start scan, receive results incrementally, stop scan
- Cancellation support (user can cancel a scan in progress)
- Deduplication of results (same host+port must not appear twice)
- Persistence of a selected discovered server via `IServerConnectionRepository` (with `DiscoveredViaMdns = true`)
- Health check validation before saving (calls `GET /global/health` via `IOpencodeApiClient`)

### Out of Scope
- Custom mDNS domain names (only default `opencode.local` / `_opencode._tcp` supported)
- Continuous background monitoring for server appearance/disappearance
- Automatic connection on discovery (user must explicitly select)
- UI implementation (separate spec)
- HTTPS discovery
- Discovery of servers outside the local network (LAN only)

---

## Functional Requirements

> Requirements are numbered for traceability.

1. **[REQ-001]** The system must define `IOpencodeDiscoveryService` with the following operations:
   - `IAsyncEnumerable<DiscoveredServerDto> ScanAsync(CancellationToken cancellationToken)` — starts a mDNS scan and yields discovered servers as they are found; completes when the scan times out or the token is cancelled
   - `Task<bool> ValidateServerAsync(DiscoveredServerDto server)` — performs a health check (`GET /global/health`) against the discovered server and returns `true` if reachable and healthy

2. **[REQ-002]** `DiscoveredServerDto` must be a `sealed record` with:
   - `string Name` — the mDNS service instance name (human-readable, e.g. `"opencode on my-macbook"`)
   - `string Host` — resolved hostname or IP address
   - `int Port` — advertised port (default `4096`)
   - `DateTimeOffset DiscoveredAt` — UTC timestamp of discovery

3. **[REQ-003]** The scan must have a configurable maximum duration. The default scan timeout is **10 seconds**. After this duration, `ScanAsync` must complete the `IAsyncEnumerable` naturally (no exception). If the `CancellationToken` is cancelled before the timeout, the scan must stop immediately.

4. **[REQ-004]** The discovery service must deduplicate results: if the same `Host` + `Port` combination is discovered more than once during a single scan, only the first occurrence must be yielded.

5. **[REQ-005]** The discovery service must query for the mDNS service type `_opencode._tcp.local.` on the local network. This is the service type advertised by `opencode serve --mdns`.

6. **[REQ-006]** When the user selects a discovered server to save, the system must:
   1. Call `ValidateServerAsync()` to confirm the server is reachable
   2. If valid, call `IServerConnectionRepository.AddAsync()` with `DiscoveredViaMdns = true` and the resolved `Host`, `Port`, and `Name`
   3. If validation fails, return an error result — do not persist the record

7. **[REQ-007]** The discovery service must not require the user to enter any credentials during the discovery phase. If the selected server requires Basic Auth, the user will be prompted to enter credentials separately (handled by the server management UI spec).

8. **[REQ-008]** `IOpencodeDiscoveryService` must be registered in `AddOpenMobCore()`. The concrete implementation must use a platform-appropriate mDNS library (see Notes for Technical Analysis).

9. **[REQ-009]** If the device has no network connectivity or mDNS is blocked on the network, `ScanAsync` must complete without throwing — it simply yields zero results. The caller (ViewModel) is responsible for showing an appropriate empty-state message.

10. **[REQ-010]** The discovery service must be independently testable: `IOpencodeDiscoveryService` must have no dependency on MAUI platform APIs directly. Any platform-specific mDNS implementation must be injected via an abstraction.

---

## Functional Impacts

### Affected Components / Systems
| Component | Impact | Notes |
|-----------|--------|-------|
| `CoreServiceExtensions` | Modified | Register `IOpencodeDiscoveryService` |
| `IServerConnectionRepository` | Consumed | Used to persist selected discovered servers |
| `IOpencodeApiClient` | Consumed | Used for health check validation in `ValidateServerAsync` |
| `MauiProgram.cs` | Modified | Register platform mDNS implementation if required |

### Dependencies
- **Depends on:** spec `2026-03-15-01` — `IServerConnectionRepository` must exist to persist discovered servers
- **Depends on:** spec `2026-03-15-02` — `IOpencodeApiClient` must exist for `ValidateServerAsync` health check
- **Required by:** Server management UI spec (future)

---

## Open Questions & Clarifications

| # | Question | Status | Answer / Decision |
|---|----------|--------|-------------------|
| 1 | What is the exact mDNS service type advertised by `opencode serve --mdns`? The docs mention `opencode.local` as the domain but do not specify the `_service._proto` type. | **Resolved** | Inspected `packages/opencode/src/server/mdns.ts` in the opencode repo. opencode uses `bonjour-service` with `type: "http"`, which advertises as `_http._tcp.local.` with service names matching the pattern `opencode-{port}`. The spec's assumed `_opencode._tcp.local.` is incorrect. The implementation must query `_http._tcp.local.` and filter results by name prefix `opencode-`. |
| 2 | Which .NET mDNS library to use on iOS and Android? Options: `Zeroconf` (NuGet), `Maui.Zeroconf`, or platform-native APIs via `DependencyService`. | **Resolved** | `Zeroconf` 3.7.16 is selected. It targets `netstandard2.0` and `net8.0` (with explicit `net8.0-ios18.0` and `net8.0-maccatalyst18.0` support), is compatible with `net10.0` (computed), and is a pure .NET library with no MAUI dependency. The implementation can live in `openMob.Core`. |
| 3 | Should the scan timeout (10s default) be user-configurable via `IOpencodeSettingsService`, or hardcoded? | **Resolved** | Hardcoded as a `const int ScanTimeoutSeconds = 10` in the implementation for v1. Can be exposed later. |
| 4 | If a previously manually-added server is later discovered via mDNS (same host+port), should the existing record be updated to set `DiscoveredViaMdns = true`? | **Resolved** | No — do not modify existing records. The service returns the discovered server as a `DiscoveredServerDto`; the UI layer (future spec) is responsible for surfacing the duplicate. |

---

## Acceptance Criteria

> Each criterion maps to one or more functional requirements.

- [ ] **[AC-001]** Given an opencode server running with `--mdns` on the local network, when `ScanAsync` is called, then the server appears in the yielded results within the scan timeout. *(REQ-001, REQ-005)*
- [ ] **[AC-002]** Given the same server is discovered twice during a single scan, when results are yielded, then only one entry for that host+port appears. *(REQ-004)*
- [ ] **[AC-003]** Given a scan in progress, when the `CancellationToken` is cancelled, then `ScanAsync` stops yielding and completes without throwing. *(REQ-003)*
- [ ] **[AC-004]** Given no opencode servers on the network, when `ScanAsync` completes after the timeout, then zero results are yielded and no exception is thrown. *(REQ-009)*
- [ ] **[AC-005]** Given a discovered server that passes `ValidateServerAsync`, when the user selects it to save, then a `ServerConnection` record with `DiscoveredViaMdns = true` is persisted in SQLite. *(REQ-006)*
- [ ] **[AC-006]** Given a discovered server that fails `ValidateServerAsync` (server unreachable), when the user attempts to save it, then no record is persisted and an error result is returned. *(REQ-006)*
- [ ] **[AC-007]** `IOpencodeDiscoveryService` is resolvable via DI without runtime exceptions on both iOS and Android. *(REQ-008)*
- [ ] **[AC-008]** Build and all tests pass with zero warnings. *(all)*

---

## Notes for Technical Analysis

> This section is addressed to the agent that will perform the technical implementation analysis.

- **mDNS service type verification:** Before implementing, confirm the exact service type advertised by opencode by inspecting the opencode source at `https://github.com/anomalyco/opencode` (search for `mdns` in the Go packages). The `--mdns-domain` flag defaults to `opencode.local` but the `_service._proto` type must be confirmed.
- **Library evaluation — priority candidates:**
  - [`Zeroconf`](https://www.nuget.org/packages/Zeroconf) — pure .NET, cross-platform, actively maintained. Likely the best fit.
  - [`Tmds.MDns`](https://www.nuget.org/packages/Tmds.MDns) — alternative, less active.
  - Platform-native (iOS `NSNetServiceBrowser`, Android `NsdManager`) via MAUI `DependencyService` — maximum control but high complexity.
  - Recommend evaluating `Zeroconf` first for .NET 10 / MAUI compatibility.
- **Abstraction pattern:** Define `IOpencodeDiscoveryService` in `openMob.Core` with no platform dependency. If the chosen mDNS library works in a pure .NET context, the implementation can also live in `openMob.Core`. If it requires platform APIs, follow the `IAppDataPathProvider` pattern: interface in Core, implementation in the MAUI project.
- **`ValidateServerAsync` implementation:** This method must construct a temporary `HttpClient` (not via `IOpencodeApiClient` which requires an active `ServerConnection`) to call `GET http://{host}:{port}/global/health`. Use `IHttpClientFactory` with a short timeout (5s) for this probe.
- **File locations:**
  - Interface: `src/openMob.Core/Infrastructure/Discovery/IOpencodeDiscoveryService.cs`
  - DTO: `src/openMob.Core/Infrastructure/Discovery/DiscoveredServerDto.cs`
  - Implementation (if pure .NET): `src/openMob.Core/Infrastructure/Discovery/OpencodeDiscoveryService.cs`
  - Implementation (if MAUI-dependent): `src/openMob/Infrastructure/Discovery/MauiOpencodeDiscoveryService.cs`
- **As established in spec `2026-03-13-project-scaffolding`**, `openMob.Core` must have zero MAUI dependencies. If the mDNS library requires platform APIs, the implementation must live in the MAUI project and be registered via `MauiProgram.cs`.
- **Related opencode docs:** `https://opencode.ai/docs/server/` — the `--mdns` and `--mdns-domain` flags are documented there.

---

## Technical Analysis

> Added by: om-orchestrator | Date: 2026-03-15

### Open Questions Resolution

All 4 open questions have been resolved through direct inspection of the opencode source code and NuGet package metadata:

1. **mDNS service type:** opencode uses `bonjour-service` (Node.js) with `type: "http"`, advertising as **`_http._tcp.local.`** with service names matching `opencode-{port}`. The implementation must query `_http._tcp.local.` and filter by name prefix `"opencode-"`.
2. **Library:** `Zeroconf` 3.7.16 — pure .NET, `netstandard2.0` + explicit `net8.0-ios18.0` support, computed `net10.0` compatibility. Implementation lives in `openMob.Core`.
3. **Scan timeout:** Hardcoded `const int ScanTimeoutSeconds = 10` in implementation for v1.
4. **Duplicate mDNS + manual record:** No update to existing records — surface as duplicate in UI (future spec).

### Change Classification

| Field | Value |
|-------|-------|
| Change type | Feature |
| Git Flow branch | feature/opencode-mdns-discovery |
| Branches from | develop |
| Estimated complexity | Medium |
| Estimated agents involved | om-mobile-core, om-tester, om-reviewer |

### Layers Involved

| Layer | Agent | Scope |
|-------|-------|-------|
| Discovery interface + DTO | om-mobile-core | `src/openMob.Core/Infrastructure/Discovery/` |
| Discovery implementation | om-mobile-core | `src/openMob.Core/Infrastructure/Discovery/` |
| DI registration | om-mobile-core | `src/openMob.Core/Infrastructure/DI/CoreServiceExtensions.cs` |
| NuGet package addition | om-mobile-core | `src/openMob.Core/openMob.Core.csproj` |
| Unit Tests | om-tester | `tests/openMob.Tests/Infrastructure/Discovery/` |
| Code Review | om-reviewer | all of the above |

### Files to Create

- `src/openMob.Core/Infrastructure/Discovery/IOpencodeDiscoveryService.cs` — interface (REQ-001)
- `src/openMob.Core/Infrastructure/Discovery/DiscoveredServerDto.cs` — sealed record DTO (REQ-002)
- `src/openMob.Core/Infrastructure/Discovery/OpencodeDiscoveryService.cs` — Zeroconf implementation (REQ-001, REQ-003, REQ-004, REQ-005, REQ-006, REQ-009)
- `tests/openMob.Tests/Infrastructure/Discovery/OpencodeDiscoveryServiceTests.cs` — unit tests

### Files to Modify

- `src/openMob.Core/openMob.Core.csproj` — add `Zeroconf` NuGet package reference
- `src/openMob.Core/Infrastructure/DI/CoreServiceExtensions.cs` — register `IOpencodeDiscoveryService` as singleton (REQ-008)

### Technical Dependencies

- `IServerConnectionRepository` (from spec `2026-03-15-01`) — ✅ completed and merged
- `IOpencodeApiClient` / `IHttpClientFactory` — ✅ completed and merged (spec `2026-03-15-02`)
- `Zeroconf` 3.7.16 NuGet package — new dependency
- `System.Reactive` 5.x (transitive dependency of Zeroconf)

### Technical Risks

- **Zeroconf on Android:** Android requires `CHANGE_WIFI_MULTICAST_STATE` permission and a `WifiManager.MulticastLock` to be held during mDNS scanning. Zeroconf handles this internally on Android via its platform-specific implementation, but the permission must be declared in `AndroidManifest.xml`. This is a **known requirement** — the implementation must document this.
- **iOS local network permission:** iOS 14+ requires `NSLocalNetworkUsageDescription` in `Info.plist` and the `_http._tcp` Bonjour service type declared in `NSBonjourServices`. This must be added to the MAUI project's platform configuration.
- **Service type filtering:** Since opencode uses the generic `_http._tcp` type (not a unique type), the scan will return ALL `_http._tcp` services on the network. The implementation must filter by name prefix `"opencode-"` to avoid false positives.
- **`ValidateServerAsync` uses `IHttpClientFactory` directly** — not `IOpencodeApiClient` — because the discovered server is not yet a registered `ServerConnection`. A named `HttpClient` with a 5-second timeout must be used.
- **No EF Core migration needed** — this feature only reads/writes via the existing `IServerConnectionRepository` interface.

### Execution Order

> Steps that can run in parallel are marked with ⟳. Steps that must be sequential are numbered.

1. [Git Flow] Create branch `feature/opencode-mdns-discovery`
2. [om-mobile-core] Add `Zeroconf` to `openMob.Core.csproj`, implement `DiscoveredServerDto`, `IOpencodeDiscoveryService`, and `OpencodeDiscoveryService`; register in `CoreServiceExtensions`
3. [om-tester] Write unit tests for `OpencodeDiscoveryService` (after step 2 completes)
4. [om-reviewer] Full review against spec
5. [Fix loop if needed] Address Critical and Major findings
6. [Git Flow] Finish branch and merge

### Definition of Done

- [ ] All `[REQ-001]` through `[REQ-010]` requirements implemented
- [ ] All `[AC-001]` through `[AC-008]` acceptance criteria satisfied
- [ ] Unit tests written for `OpencodeDiscoveryService` covering happy path, cancellation, timeout, deduplication, validation failure, and network error
- [ ] `om-reviewer` verdict: ✅ Approved or ⚠️ Approved with remarks
- [ ] Git Flow branch finished and deleted
- [ ] Spec moved to `specs/done/` with Completed status
