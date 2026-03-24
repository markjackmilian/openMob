# Replace EF Core with sqlite-net-pcl

## Metadata
| Field       | Value                                              |
|-------------|----------------------------------------------------|
| Date        | 2026-03-24                                         |
| Status      | **Completed**                                      |
| Version     | 1.0                                                |
| Completed   | 2026-03-24                                         |
| Branch      | bugfix/ios-release-startup-crash (merged)          |
| Merged into | develop                                            |

---

## Executive Summary

EF Core is currently used as the sole data access layer for openMob, managing three simple tables (`ServerConnections`, `ProjectPreferences`, `AppStates`) with no relational joins. Its overhead — compiled model regeneration on every migration, 12 migration files, 6 generated CompiledModel files, and a mandatory two-command workflow after every schema change — is disproportionate to the actual data access needs of the app. This spec defines the replacement of EF Core with `sqlite-net-pcl`, a minimal ORM designed for mobile apps, which provides automatic schema migration (add column/table) via `CreateTableAsync<T>()` with zero tooling and zero generated files.

---

## Scope

### In Scope
- Remove `Microsoft.EntityFrameworkCore.Sqlite` and `Microsoft.EntityFrameworkCore.Design` from `openMob.Core.csproj`
- Remove `Microsoft.EntityFrameworkCore.Sqlite` and `Microsoft.Data.Sqlite.Core` from `openMob.Tests.csproj`
- Delete `Data/AppDbContext.cs`, `Data/AppDbContextFactory.cs`
- Delete all files under `Data/Migrations/` (12 files + snapshot)
- Delete all files under `Data/CompiledModels/` (6 files)
- Add `sqlite-net-pcl` to `openMob.Core.csproj`
- Introduce `IAppDatabase` interface (Singleton) as the single entry point to the SQLite connection
- Implement `AppDatabase` using `SQLiteAsyncConnection` from `sqlite-net-pcl`
- Rewrite `ServerConnectionRepository` using `sqlite-net-pcl` API — preserving `IServerConnectionRepository` contract unchanged
- Rewrite `AppStateService` using `IAppDatabase` — preserving `IAppStateService` contract unchanged
- Rewrite `ProjectPreferenceService` using `IAppDatabase` — preserving `IProjectPreferenceService` contract unchanged
- Update `CoreServiceExtensions.cs`: remove `AddDbContext<AppDbContext>()`, register `IAppDatabase` as Singleton
- Update `MauiProgram.cs`: remove `db.Database.Migrate()`, call `IAppDatabase.InitializeAsync()` at startup
- Rewrite `ServerConnectionRepositoryTests` without EF Core / `SqliteConnection` in-memory
- Remove `IServiceScopeFactory` workaround from `AppStateService` (no longer needed — `IAppDatabase` is Singleton)
- Remove `AppDbContext` direct injection from `ProjectPreferenceService`
- The new DB file name remains `openmob.db` in the same `IAppDataPathProvider.AppDataPath` directory
- Since migration from existing data is explicitly out of scope, the app starts from a clean DB

### Out of Scope
- Migration of existing user data from the EF Core schema to the new schema
- Changes to any public service interface (`IServerConnectionRepository`, `IAppStateService`, `IProjectPreferenceService`)
- Changes to any ViewModel, View, or XAML
- Changes to `IAppDataPathProvider` or `MauiAppDataPathProvider`
- Changes to `IServerCredentialStore` or `MauiServerCredentialStore`
- Introduction of any additional ORM or query library (Dapper, FluentMigrator, etc.)
- Encryption of the SQLite database

---

## Functional Requirements

> Requirements are numbered for traceability.

1. **[REQ-001]** The app MUST open (or create) the SQLite database file at `Path.Combine(IAppDataPathProvider.AppDataPath, "openmob.db")` on startup, using `sqlite-net-pcl`'s `SQLiteAsyncConnection`.

2. **[REQ-002]** On startup, the app MUST call `CreateTableAsync<T>()` for each entity (`ServerConnection`, `ProjectPreference`, `AppState`). If the table does not exist it is created; if it exists, `sqlite-net-pcl` automatically adds any missing columns via `ALTER TABLE ADD COLUMN`.

3. **[REQ-003]** The `IAppDatabase` interface MUST be registered as a **Singleton** in the DI container. All repositories and services that previously injected `AppDbContext` MUST inject `IAppDatabase` instead.

4. **[REQ-004]** `IServerConnectionRepository` contract MUST remain identical. The implementation MUST be rewritten using `sqlite-net-pcl` async API (`InsertAsync`, `UpdateAsync`, `DeleteAsync`, `Table<T>().Where(...)`, `RunInTransactionAsync`).

5. **[REQ-005]** The single-active-connection constraint (previously enforced via EF Core transaction + raw SQL) MUST be preserved. The new implementation MUST use `RunInTransactionAsync` to atomically deactivate all other connections and activate the target one.

6. **[REQ-006]** `IAppStateService` contract MUST remain identical. The `IServiceScopeFactory` workaround MUST be removed; `AppStateService` MUST inject `IAppDatabase` directly (Singleton-safe).

7. **[REQ-007]** `IProjectPreferenceService` contract MUST remain identical. `ProjectPreferenceService` MUST inject `IAppDatabase` directly instead of `AppDbContext`.

8. **[REQ-008]** `MauiProgram.cs` MUST call `await appDatabase.InitializeAsync()` (or equivalent sync call) after `builder.Build()`, replacing the current `db.Database.Migrate()` block. The call MUST be wrapped in try/catch with Sentry capture on failure.

9. **[REQ-009]** The `Data/Migrations/` directory and `Data/CompiledModels/` directory MUST be fully deleted. No EF Core migration file or compiled model file may remain in the repository.

10. **[REQ-010]** The `AGENTS.md` section on EF Core migrations (the `ef-migrations` skill reference and the `dotnet ef` commands) MUST be updated to reflect the new schema evolution workflow: "add property to C# class → `CreateTableAsync` handles it automatically at next startup."

11. **[REQ-011]** All existing unit tests for `ServerConnectionRepository` MUST be rewritten to use `sqlite-net-pcl` with an in-process SQLite file (temp path) or in-memory connection, without any EF Core dependency. All previously passing test cases MUST continue to pass.

12. **[REQ-012]** `openMob.Tests.csproj` MUST remove `Microsoft.EntityFrameworkCore.Sqlite` and `Microsoft.Data.Sqlite.Core` package references, and add `sqlite-net-pcl`.

13. **[REQ-013]** `DateTime` values MUST be stored as ticks (`storeDateTimeAsTicks: true`) in the `SQLiteAsyncConnection` constructor, consistent with `sqlite-net-pcl` best practices for performance and precision.

14. **[REQ-014]** The `ThinkingLevel` enum stored in `ProjectPreference` MUST be persisted as its integer value. The `sqlite-net-pcl` entity class MUST use `[Column]` or store it as `int` explicitly, since `sqlite-net-pcl` does not automatically convert enums.

---

## Functional Impacts

### Affected Components / Systems
| Component | Impact | Notes |
|-----------|--------|-------|
| `openMob.Core.csproj` | Modified | Remove EF Core packages, add `sqlite-net-pcl` |
| `openMob.Tests.csproj` | Modified | Remove EF Core + `Microsoft.Data.Sqlite.Core`, add `sqlite-net-pcl` |
| `Data/AppDbContext.cs` | Deleted | Replaced by `AppDatabase.cs` |
| `Data/AppDbContextFactory.cs` | Deleted | No longer needed |
| `Data/Migrations/` (12 files) | Deleted | Replaced by `CreateTableAsync` auto-migration |
| `Data/CompiledModels/` (6 files) | Deleted | No longer needed |
| `Data/IAppDatabase.cs` | Created | New Singleton interface for DB access |
| `Data/AppDatabase.cs` | Created | `sqlite-net-pcl` implementation |
| `Data/Repositories/ServerConnectionRepository.cs` | Rewritten | Same interface, new implementation |
| `Services/AppStateService.cs` | Rewritten | Remove `IServiceScopeFactory`, inject `IAppDatabase` |
| `Services/ProjectPreferenceService.cs` | Rewritten | Remove `AppDbContext`, inject `IAppDatabase` |
| `Infrastructure/DI/CoreServiceExtensions.cs` | Modified | Remove `AddDbContext`, register `IAppDatabase` as Singleton |
| `MauiProgram.cs` | Modified | Replace `db.Database.Migrate()` with `InitializeAsync()` |
| `tests/.../ServerConnectionRepositoryTests.cs` | Rewritten | Remove EF Core, use `sqlite-net-pcl` |
| `tests/.../Helpers/TestDbContextFactory.cs` | Deleted | No longer needed |
| `AGENTS.md` | Modified | Update EF Core migration section |

### Dependencies
- `sqlite-net-pcl` NuGet package (latest stable, Frank Krueger) — replaces EF Core as the SQLite access layer
- `SQLitePCLRaw.bundle_green` (transitive dependency of `sqlite-net-pcl`) — provides the native SQLite bindings for iOS and Android
- `SQLitePCLRaw.bundle_e_sqlite3` — used in the test project for Windows desktop test runner compatibility
- `IAppDataPathProvider` — unchanged, still provides the DB file path
- `IServerCredentialStore` — unchanged, still manages passwords in SecureStorage

---

## Open Questions & Clarifications

| # | Question | Status | Answer / Decision |
|---|----------|--------|-------------------|
| 1 | Partire da DB pulito o gestire migrazione dati utenti esistenti? | Resolved | Partire da DB pulito — nessuna migrazione dati |
| 2 | Quale libreria SQLite leggera usare? | Resolved | `sqlite-net-pcl` — progettata per mobile, auto-migrazione colonne via `CreateTableAsync` |
| 3 | Come gestire l'evoluzione futura dello schema? | Resolved | Aggiungere proprietà alla classe C# → `CreateTableAsync` esegue `ALTER TABLE ADD COLUMN` automaticamente al prossimo avvio |
| 4 | Come gestire il tipo enum `ThinkingLevel` senza EF Core? | Resolved | Storare come `int` esplicitamente nella classe entità; `sqlite-net-pcl` non converte enum automaticamente |
| 5 | `IAppDatabase` deve essere Singleton o Scoped? | Resolved | Singleton — `SQLiteAsyncConnection` è thread-safe e progettata per essere condivisa; elimina il problema captive dependency che richiedeva `IServiceScopeFactory` in `AppStateService` |
| 6 | Il nome del file DB cambia? | Resolved | No — rimane `openmob.db` nello stesso path |

---

## Acceptance Criteria

> Each criterion maps to one or more functional requirements.

- [x] **[AC-001]** Given a fresh install, when the app starts, then `openmob.db` is created in `IAppDataPathProvider.AppDataPath` and all three tables (`ServerConnections`, `ProjectPreferences`, `AppStates`) exist. *(REQ-001, REQ-002)*

- [x] **[AC-002]** Given a DB with an existing `ServerConnections` table missing a column, when the app starts, then `sqlite-net-pcl` adds the missing column automatically without errors. *(REQ-002)*

- [x] **[AC-003]** Given a valid `ServerConnectionDto`, when `AddAsync` is called, then the record is persisted and the returned DTO has a new ULID id and correct `CreatedAt`/`UpdatedAt` timestamps. *(REQ-004)*

- [x] **[AC-004]** Given two active connections, when `SetActiveAsync(id)` is called, then only the target connection has `IsActive = true` and all others have `IsActive = false`, enforced atomically. *(REQ-004, REQ-005)*

- [x] **[AC-005]** Given `SetActiveAsync` called with a non-existent ID, when the transaction rolls back, then previously active connections remain active. *(REQ-005)*

- [x] **[AC-006]** Given `AppStateService` is registered as Singleton, when `SetLastActiveProjectIdAsync` is called, then the value is persisted and `GetLastActiveProjectIdAsync` returns it — without any `IServiceScopeFactory` indirection. *(REQ-003, REQ-006)*

- [x] **[AC-007]** Given a `ProjectPreference` that does not exist, when `GetOrDefaultAsync` is called, then a transient default is returned without inserting a row in the DB. *(REQ-007)*

- [x] **[AC-008]** Given the solution builds with `dotnet build openMob.sln`, then zero EF Core references remain in the build output and no `dotnet ef` commands are required. *(REQ-009)*

- [x] **[AC-009]** Given `dotnet test tests/openMob.Tests/openMob.Tests.csproj`, then all `ServerConnectionRepositoryTests` pass without any EF Core or `Microsoft.Data.Sqlite.Core` dependency. *(REQ-011, REQ-012)*

- [x] **[AC-010]** Given a `ThinkingLevel` enum value is saved via `ProjectPreferenceService`, when it is read back, then the correct enum value is returned. *(REQ-014)*

- [x] **[AC-011]** Given the app starts and `InitializeAsync()` throws, then the exception is captured to Sentry and the app does not crash with an unhandled exception. *(REQ-008)*

---

## Technical Analysis

> Added by: om-orchestrator | Date: 2026-03-24

### Change Classification

| Field | Value |
|-------|-------|
| Change type | Refactoring (infrastructure replacement) |
| Git Flow branch | bugfix/ios-release-startup-crash |
| Branches from | develop |
| Estimated complexity | High |
| Agents involved | om-orchestrator (direct implementation) |

### Implementation Notes

- `IAppDatabase` exposes `SQLiteAsyncConnection Connection { get; }` directly — no re-wrapping of every API method. The interface exists for DI registration and NSubstitute testability.
- `SQLiteAsyncConnection` is thread-safe by design; sharing a single Singleton instance across all repositories is safe.
- `RunInTransactionAsync` rollback for `SetActiveAsync`: throws a sentinel `InvalidOperationException("__rollback__:{id}")` inside the transaction lambda to abort; caught and swallowed outside to return `false`.
- Test isolation: `:memory:` SQLite connections are not safe for parallel xUnit tests (shared state across test instances). Solution: per-test temp file (`Path.GetTempPath() + Guid.NewGuid()`) with cleanup in `DisposeAsync`.
- `SQLitePCLRaw.bundle_green` for MAUI (iOS/Android native bindings); `SQLitePCLRaw.bundle_e_sqlite3` for the test project (Windows desktop runner).
- `[Preserve(AllMembers = true)]` on all entity classes prevents iOS Release linker from stripping property metadata used by sqlite-net-pcl reflection.

### Build & Test Results

| Check | Result |
|-------|--------|
| `dotnet build openMob.Core.csproj` | ✅ 0 errors, 0 warnings |
| `ServerConnectionRepositoryTests` (26 tests) | ✅ 26/26 passed |
| `AppStateServiceTests` (14 tests) | ✅ 14/14 passed |
| `ProjectPreferenceServiceTests` (43 tests) | ✅ 43/43 passed |
| Full test suite | ✅ 1144/1148 (4 pre-existing failures unrelated to this change) |
