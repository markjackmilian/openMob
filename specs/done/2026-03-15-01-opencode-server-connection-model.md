# opencode Server Connection Model

## Metadata
| Field       | Value                                          |
|-------------|------------------------------------------------|
| Date        | 2026-03-15                                     |
| Status      | **Completed**                                  |
| Version     | 1.0                                            |
| Completed   | 2026-03-15                                     |
| Branch      | feature/server-connection-model (merged)       |
| Merged into | develop                                        |

---

## Executive Summary

This spec defines the persistence layer for opencode server connection records. It introduces the `ServerConnection` EF Core entity, the corresponding SQLite migration, and the `SecureStorage` strategy for storing server credentials. This is the foundational prerequisite for all subsequent opencode API client and discovery features.

---

## Scope

### In Scope
- `ServerConnection` EF Core entity model with all required fields
- `DbSet<ServerConnection>` registration in `AppDbContext`
- EF Core migration for the new table
- `IServerConnectionRepository` interface and implementation for CRUD operations
- `IServerCredentialStore` interface for reading/writing credentials via `SecureStorage`
- Registration of new services in `AddOpenMobCore()`
- DTO mapping: `ServerConnectionDto` (sealed record)

### Out of Scope
- UI for managing server connections (separate spec)
- mDNS discovery logic (spec `2026-03-15-03`)
- HTTP client implementation (spec `2026-03-15-02`)
- Any business logic for connecting to or validating a server

---

## Functional Requirements

> Requirements are numbered for traceability.

1. **[REQ-001]** The system must define a `ServerConnection` entity with the following fields:
   - `Id` (`string`, ULID or GUID, primary key)
   - `Name` (`string`, user-defined label, max 100 chars, not null)
   - `Host` (`string`, hostname or IP address, not null)
   - `Port` (`int`, default `4096`)
   - `Username` (`string`, nullable — only set when server has Basic Auth enabled)
   - `IsActive` (`bool`, only one record may have `IsActive = true` at any time)
   - `DiscoveredViaMdns` (`bool`, true if the record was created via mDNS discovery)
   - `CreatedAt` (`DateTimeOffset`, UTC, set on insert)
   - `UpdatedAt` (`DateTimeOffset`, UTC, updated on every save)

2. **[REQ-002]** The password associated with a `ServerConnection` must **never** be stored in the SQLite database. It must be stored exclusively in `SecureStorage` using a key derived from the connection `Id` (e.g., `opencode_server_pwd_{id}`).

3. **[REQ-003]** `AppDbContext` must expose a `DbSet<ServerConnection> ServerConnections` property.

4. **[REQ-004]** An EF Core migration must be created to add the `ServerConnections` table. The migration must be applied at startup via the existing `db.Database.Migrate()` call in `MauiProgram.cs`.

5. **[REQ-005]** The system must enforce the single-active-server constraint at the repository level: when a `ServerConnection` is set as active, all other records must have `IsActive` set to `false` within the same database transaction.

6. **[REQ-006]** The system must define `IServerConnectionRepository` with the following operations:
   - `GetAllAsync()` → `IReadOnlyList<ServerConnectionDto>`
   - `GetActiveAsync()` → `ServerConnectionDto?`
   - `GetByIdAsync(string id)` → `ServerConnectionDto?`
   - `AddAsync(ServerConnectionDto dto)` → `ServerConnectionDto`
   - `UpdateAsync(ServerConnectionDto dto)` → `ServerConnectionDto`
   - `DeleteAsync(string id)` → `bool`
   - `SetActiveAsync(string id)` → `bool`

7. **[REQ-007]** The system must define `IServerCredentialStore` with the following operations:
   - `SavePasswordAsync(string connectionId, string password)` → `Task`
   - `GetPasswordAsync(string connectionId)` → `Task<string?>`
   - `DeletePasswordAsync(string connectionId)` → `Task`

8. **[REQ-008]** When a `ServerConnection` is deleted via `IServerConnectionRepository.DeleteAsync()`, the corresponding password entry in `SecureStorage` must also be deleted automatically (coordinated by the repository or a domain service).

9. **[REQ-009]** `ServerConnectionDto` must be a `sealed record` containing all fields of the entity **except** the password. It must include a `bool HasPassword` computed field (true if a password exists in `SecureStorage` for that connection ID).

10. **[REQ-010]** All new services (`IServerConnectionRepository`, `IServerCredentialStore`) must be registered in `CoreServiceExtensions.AddOpenMobCore()` with appropriate lifetimes (scoped for repository, singleton for credential store).

---

## Functional Impacts

### Affected Components / Systems
| Component | Impact | Notes |
|-----------|--------|-------|
| `AppDbContext` | Modified | Add `DbSet<ServerConnection>` |
| `CoreServiceExtensions` | Modified | Register new services |
| `MauiProgram.cs` | Modified | Register `MauiServerCredentialStore` for `IServerCredentialStore` |
| `SecureStorage` | New usage | Credential storage keyed by connection ID |
| EF Core Migrations | New migration | `AddServerConnectionsTable` |

### Dependencies
- Spec `2026-03-15-02` (opencode API Client) depends on this spec being completed first — it reads `ServerConnection` to resolve `host`, `port`, and credentials at runtime.
- Spec `2026-03-15-03` (mDNS Discovery) depends on this spec to persist discovered servers.

---

## Open Questions & Clarifications

| # | Question | Status | Answer / Decision |
|---|----------|--------|-------------------|
| 1 | Should `Id` use ULID (sortable) or standard GUID? | Resolved | ULID — provides natural time-ordering for list views. Add `Ulid` NuGet package to `openMob.Core`. |
| 2 | Should `IServerCredentialStore` be a thin wrapper over `SecureStorage` or abstract it fully for testability? | Resolved | Full abstraction via interface — follows existing `IAppDataPathProvider` pattern. Interface in Core, MAUI implementation in the app project. |
| 3 | Should `DeleteAsync` fail silently if the `SecureStorage` entry is missing, or throw? | Resolved | Silent no-op (idempotent delete) — standard pattern for credential cleanup. |

---

## Acceptance Criteria

> Each criterion maps to one or more functional requirements.

- [x] **[AC-001]** Given a new app install, when the app starts, then the `ServerConnections` table exists in the SQLite database. *(REQ-001, REQ-004)* — Verified via `adb exec-out` + `sqlite3`: table exists with correct schema and index.
- [x] **[AC-002]** Given a `ServerConnection` with a password, when it is saved, then the password is retrievable from `SecureStorage` and absent from the `ServerConnections` SQLite table. *(REQ-002)* — Verified: no password column in entity/DB. SecureStorage key format `opencode_server_pwd_{id}`.
- [x] **[AC-003]** Given two server connections, when `SetActiveAsync` is called on one, then only that connection has `IsActive = true` and the other has `IsActive = false`. *(REQ-005)* — Verified by unit test `SetActiveAsync_WhenOtherConnectionsActive_DeactivatesThem`.
- [x] **[AC-004]** Given a `ServerConnection` that is deleted, when `DeleteAsync` completes, then both the DB row and the `SecureStorage` password entry are removed. *(REQ-008)* — Verified by unit test `DeleteAsync_WhenConnectionExists_DeletesCredentialFromStore`.
- [x] **[AC-005]** Given a `ServerConnectionDto`, when `HasPassword` is checked, then it returns `true` only if a password exists in `SecureStorage` for that connection ID. *(REQ-009)* — Verified by unit tests `GetAllAsync_WhenPasswordExists_SetsHasPasswordTrue` and `_WhenNoPasswordExists_SetsHasPasswordFalse`.
- [x] **[AC-006]** All new services are resolvable via DI without runtime exceptions. *(REQ-010)* — Verified: app launches without DI errors on Android device.
- [x] **[AC-007]** Build and all existing tests pass with zero warnings after the migration is added. *(REQ-004)* — Verified: `dotnet build` 0 errors 0 warnings, `dotnet test` 21/21 pass.

---

## Notes for Technical Analysis

> This section is addressed to the agent that will perform the technical implementation analysis.

- **Existing pattern to follow:** `AppDbContext` at `src/openMob.Core/Data/AppDbContext.cs` — add `DbSet<ServerConnection>` alongside the placeholder comment at line 34. Follow the same file-scoped namespace and sealed class conventions.
- **Migration command:**
  ```bash
  dotnet ef migrations add AddServerConnectionsTable \
    --project src/openMob.Core/openMob.Core.csproj \
    --startup-project src/openMob/openMob.csproj
  ```
- **SecureStorage abstraction:** `IServerCredentialStore` must live in `openMob.Core` with zero MAUI dependencies. The concrete `MauiServerCredentialStore` (using `Microsoft.Maui.Storage.SecureStorage`) must live in the `openMob` MAUI project and be registered in `MauiProgram.cs`, following the same pattern as `IAppDataPathProvider` / `MauiAppDataPathProvider`.
- **Single-active constraint:** Implement as an explicit `UPDATE ServerConnections SET IsActive = 0 WHERE Id != @id` within a `BeginTransactionAsync` block in the repository, not via EF Core change tracking alone.
- **DTO location:** `src/openMob.Core/Infrastructure/Dtos/ServerConnectionDto.cs`
- **Repository location:** `src/openMob.Core/Data/Repositories/`
- **Credential store interface location:** `src/openMob.Core/Infrastructure/Security/IServerCredentialStore.cs`
- **MAUI implementation location:** `src/openMob/Infrastructure/Security/MauiServerCredentialStore.cs`
- **Related files:** `src/openMob.Core/Data/AppDbContext.cs`, `src/openMob/MauiProgram.cs`, `src/openMob.Core/Infrastructure/DI/CoreServiceExtensions.cs`

---

## Technical Analysis

> Added by: om-orchestrator | Date: 2026-03-15

### Change Classification

| Field | Value |
|-------|-------|
| Change type | Feature |
| Git Flow branch | feature/server-connection-model |
| Branches from | develop |
| Estimated complexity | Medium |
| Estimated agents involved | om-mobile-core, om-tester, om-reviewer |

### Layers Involved

| Layer | Agent | Scope |
|-------|-------|-------|
| Data / EF Core entity + migration | om-mobile-core | `src/openMob.Core/Data/` |
| Repository (CRUD) | om-mobile-core | `src/openMob.Core/Data/Repositories/` |
| Security abstraction | om-mobile-core | `src/openMob.Core/Infrastructure/Security/` |
| MAUI credential store impl | om-mobile-core | `src/openMob/Infrastructure/Security/` |
| DTO mapping | om-mobile-core | `src/openMob.Core/Infrastructure/Dtos/` |
| DI registration | om-mobile-core | `src/openMob.Core/Infrastructure/DI/`, `src/openMob/MauiProgram.cs` |
| Unit Tests | om-tester | `tests/openMob.Tests/` |
| Code Review | om-reviewer | all of the above |

### Files to Create

- `src/openMob.Core/Data/Entities/ServerConnection.cs` — EF Core entity (REQ-001)
- `src/openMob.Core/Data/Repositories/IServerConnectionRepository.cs` — repository interface (REQ-006)
- `src/openMob.Core/Data/Repositories/ServerConnectionRepository.cs` — repository implementation (REQ-005, REQ-006, REQ-008)
- `src/openMob.Core/Infrastructure/Security/IServerCredentialStore.cs` — credential store interface (REQ-007)
- `src/openMob.Core/Infrastructure/Dtos/ServerConnectionDto.cs` — sealed record DTO (REQ-009)
- `src/openMob/Infrastructure/Security/MauiServerCredentialStore.cs` — MAUI SecureStorage implementation (REQ-002, REQ-007)
- `src/openMob.Core/Data/Migrations/<timestamp>_AddServerConnectionsTable.cs` — EF Core migration (REQ-004)
- `tests/openMob.Tests/Data/Repositories/ServerConnectionRepositoryTests.cs` — repository unit tests
- `tests/openMob.Tests/Helpers/InMemoryServerCredentialStore.cs` — test double for IServerCredentialStore

### Files to Modify

- `src/openMob.Core/Data/AppDbContext.cs` — add `DbSet<ServerConnection>`, add entity configuration in `OnModelCreating` (REQ-003)
- `src/openMob.Core/Infrastructure/DI/CoreServiceExtensions.cs` — register `IServerConnectionRepository` (scoped) (REQ-010)
- `src/openMob/MauiProgram.cs` — register `MauiServerCredentialStore` as singleton for `IServerCredentialStore` (REQ-010)
- `src/openMob.Core/openMob.Core.csproj` — add `Ulid` NuGet package reference
- `tests/openMob.Tests/GlobalUsings.cs` — add global usings for new namespaces
- `tests/openMob.Tests/Helpers/TestDataBuilder.cs` — add `CreateServerConnection()` and `CreateServerConnectionDto()` factory methods

### Technical Dependencies

- **No prerequisite features** — this is the foundational data layer spec.
- **No Claude server API endpoints** — this spec is purely persistence/storage.
- **New NuGet package:** `Ulid` (for ULID generation in `ServerConnection.Id`)
- **EF Core migration tooling** — `dotnet ef` CLI must be available to generate the migration.
- **EF Core `Microsoft.EntityFrameworkCore.Sqlite` 9.x** — already in `openMob.Core.csproj`.
- **Testing note:** `ServerConnectionRepository` depends on `AppDbContext` (EF Core). Tests will use EF Core's `UseInMemoryDatabase` or `UseSqlite` with an in-memory connection for true integration-style tests. Since the repository contains raw SQL (`UPDATE ... SET IsActive = 0`), an in-memory SQLite provider is required — the EF Core InMemory provider does not support raw SQL. Add `Microsoft.EntityFrameworkCore.Sqlite` to the test project.

### Technical Risks

- **EF Core migration generation** requires the MAUI startup project to be buildable. If the MAUI project has platform-specific build issues on the current machine, the migration must be generated on a machine with the MAUI workload installed. Mitigation: the migration can be hand-crafted if `dotnet ef` fails.
- **`SecureStorage` platform differences:** iOS uses Keychain, Android uses EncryptedSharedPreferences. The `IServerCredentialStore` abstraction fully isolates this. No platform-conditional code needed in Core.
- **Single-active constraint race condition:** If two concurrent calls to `SetActiveAsync` occur, the transaction isolation level must prevent both from succeeding. SQLite's default serialized write behavior mitigates this, but the repository must use `BeginTransactionAsync` explicitly.
- **`DateTimeOffset` in SQLite:** EF Core stores `DateTimeOffset` as TEXT in SQLite. This is acceptable but means sorting by `CreatedAt` in raw SQL requires awareness of the ISO 8601 format. No risk for this spec since we use EF Core LINQ queries.

### Execution Order

> Steps that can run in parallel are marked with ⟳. Steps that must be sequential are numbered.

1. **[Git Flow]** Create branch `feature/server-connection-model` from `develop`
2. **[om-mobile-core]** Implement entity, repository, credential store interface, DTO, DI registration, MAUI implementation, and generate EF Core migration
3. **[om-tester]** Write unit tests for `ServerConnectionRepository` (using in-memory SQLite) and integration with `IServerCredentialStore` mock
4. **[om-reviewer]** Full review against spec — all REQ and AC items
5. **[Fix loop if needed]** Address Critical and Major findings
6. **[Git Flow]** Finish branch and merge into `develop`

> Note: No om-mobile-ui involvement — this spec has no UI components.

### Definition of Done

- [x] All `[REQ-001]` through `[REQ-010]` requirements implemented
- [x] All `[AC-001]` through `[AC-007]` acceptance criteria satisfied
- [x] Unit tests written for `ServerConnectionRepository` — 21 tests covering all CRUD, single-active, credential cleanup, error paths
- [x] `om-reviewer` verdict: ✅ Approved (zero findings after fix loop)
- [x] `dotnet build openMob.sln` — zero errors
- [x] `dotnet test` — 21/21 tests pass
- [x] Git Flow branch finished and deleted
- [x] Spec moved to `specs/done/` with Completed status
