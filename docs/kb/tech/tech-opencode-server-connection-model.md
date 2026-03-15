# Technical Analysis — opencode Server Connection Model
**Feature slug:** opencode-server-connection-model
**Completed:** 2026-03-15
**Branch:** feature/server-connection-model
**Complexity:** Medium

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

### Files Created

- `src/openMob.Core/Data/Entities/ServerConnection.cs` — EF Core entity (REQ-001)
- `src/openMob.Core/Data/Repositories/IServerConnectionRepository.cs` — repository interface (REQ-006)
- `src/openMob.Core/Data/Repositories/ServerConnectionRepository.cs` — repository implementation (REQ-005, REQ-006, REQ-008)
- `src/openMob.Core/Infrastructure/Security/IServerCredentialStore.cs` — credential store interface (REQ-007)
- `src/openMob.Core/Infrastructure/Dtos/ServerConnectionDto.cs` — sealed record DTO (REQ-009)
- `src/openMob/Infrastructure/Security/MauiServerCredentialStore.cs` — MAUI SecureStorage implementation (REQ-002, REQ-007)
- `src/openMob.Core/Data/Migrations/20260315000000_AddServerConnectionsTable.cs` — EF Core migration (REQ-004)
- `tests/openMob.Tests/Data/Repositories/ServerConnectionRepositoryTests.cs` — 21 unit tests
- `tests/openMob.Tests/Helpers/InMemoryServerCredentialStore.cs` — test double for IServerCredentialStore
- `tests/openMob.Tests/Helpers/TestDbContextFactory.cs` — in-memory SQLite context factory

### Files Modified

- `src/openMob.Core/Data/AppDbContext.cs` — added `DbSet<ServerConnection>`, entity configuration in `OnModelCreating`
- `src/openMob.Core/Infrastructure/DI/CoreServiceExtensions.cs` — registered `IServerConnectionRepository` (scoped)
- `src/openMob/MauiProgram.cs` — registered `MauiServerCredentialStore` (singleton), restored AppShell/MainPage DI
- `src/openMob.Core/openMob.Core.csproj` — added `Ulid` NuGet package, `InternalsVisibleTo`
- `src/openMob/Resources/Styles/Colors.xaml` — fixed AppThemeBinding crash (pre-existing bug)
- `src/openMob/Resources/Styles/Styles.xaml` — fixed AppThemeBinding crash (pre-existing bug)
- `src/openMob/App.xaml.cs` — deferred AppShell to CreateWindow() via IServiceProvider
- `src/openMob/openMob.csproj` — disabled MauiXamlInflator=SourceGen

### Technical Decisions Made During Implementation

1. **DateTime over DateTimeOffset** — Changed from `DateTimeOffset` to `DateTime` (UTC) for SQLite compatibility. See ADR `adr-datetime-over-datetimeoffset-sqlite.md`.
2. **ULID for primary keys** — Chose ULID over GUID for natural time-ordering. See ADR `adr-ulid-primary-keys.md`.
3. **Platform service abstraction** — Interface in Core, MAUI impl in app project. See ADR `adr-platform-service-abstraction-pattern.md`.
4. **Credential deletion before DB deletion** — In `DeleteAsync`, credential is deleted first (idempotent) to prevent orphaned secrets if app crashes mid-operation.
5. **UpdateAsync excludes IsActive** — By design, `IsActive` can only change via `SetActiveAsync` to enforce single-active constraint.
6. **In-memory SQLite for tests** — Required because repository uses raw SQL (`ExecuteSqlInterpolatedAsync`) which EF Core InMemory provider doesn't support.

### Review Findings and Resolutions

| Finding | Severity | Resolution |
|---------|----------|------------|
| [M-001] DeleteAsync credential ordering | Major | Reordered: credential deleted before DB row |
| [M-002] DateTimeOffset SQLite incompatibility | Major | Changed to DateTime UTC throughout |
| [M-003] UpdateAsync silently ignores IsActive | Major | Documented in XML docs as intentional |
| [m-001] Missing CancellationToken on IServerCredentialStore | Minor | Added to all 3 methods + all call sites |
| [m-002] Raw SQL deviation from spec | Minor | Changed to ExecuteSqlInterpolatedAsync with WHERE Id != {id} |
| [m-003] DbSet expression-bodied property | Minor | Changed to { get; set; } = null!; |
| [m-004] Missing constructor null guards | Minor | Added ArgumentNullException.ThrowIfNull |
| [m-005] Missing input validation in MauiServerCredentialStore | Minor | Added ThrowIfNullOrWhiteSpace guards |
