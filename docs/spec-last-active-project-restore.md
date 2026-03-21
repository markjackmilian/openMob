# Last Active Project Restore on Startup

## Metadata
| Field       | Value                                          |
|-------------|------------------------------------------------|
| Date        | 2026-03-21                                     |
| Status      | **Completed**                                  |
| Version     | 1.0                                            |
| Completed   | 2026-03-21                                     |
| Branch      | feature/last-active-project-restore (merged)   |

---

## Executive Summary

At app startup, the last active project is now restored from SQLite persistence. A new `AppState` key-value table stores the `LastActiveProjectId`. `ActiveProjectService.SetActiveProjectAsync` automatically persists the project ID on every change. `SplashViewModel` reads the saved ID on startup and restores the project, falling back to the first available project if the saved one no longer exists.

---

## Key Decisions

1. **New `AppState` key-value table** — No existing config table existed in SQLite. Created a dedicated `AppState` entity with `Key TEXT PRIMARY KEY, Value TEXT NULL` schema. This is reusable for future global app state persistence.

2. **Persistence in `ActiveProjectService` (not in ViewModels)** — Project changes happen in 3+ ViewModels (`FlyoutViewModel`, `ProjectSwitcherViewModel`, `ProjectDetailViewModel`), all through `IActiveProjectService.SetActiveProjectAsync`. Integrating persistence at the service level ensures all change points are covered without modifying each ViewModel.

3. **Singleton + IServiceScopeFactory pattern** — `IAppStateService` is Singleton (global state), but `AppDbContext` is Scoped. Used `IServiceScopeFactory` to create per-operation scopes, consistent with the established codebase pattern.

4. **Non-fatal restore on startup** — `SplashViewModel.RestoreActiveProjectAsync` catches all exceptions and logs to Sentry. If project restore fails, startup continues normally — the feature is additive and must not break the existing startup flow.

---

## Files Created

- `src/openMob.Core/Data/Entities/AppState.cs` — Key-value entity
- `src/openMob.Core/Data/Migrations/20260321000000_AddAppStateTable.cs` + Designer — EF Core migration
- `src/openMob.Core/Services/IAppStateService.cs` — Interface
- `src/openMob.Core/Services/AppStateService.cs` — Singleton implementation with IServiceScopeFactory
- `tests/openMob.Tests/Services/AppStateServiceTests.cs` — 14 unit tests

## Files Modified

- `src/openMob.Core/Data/AppDbContext.cs` — Added `DbSet<AppState>` + entity config
- `src/openMob.Core/Services/ActiveProjectService.cs` — Injected `IAppStateService`, persist on SetActive
- `src/openMob.Core/ViewModels/SplashViewModel.cs` — Added project restore logic with fallback chain
- `src/openMob.Core/Infrastructure/DI/CoreServiceExtensions.cs` — Registered `IAppStateService` as Singleton
- `tests/openMob.Tests/ViewModels/SplashViewModelTests.cs` — 5 new tests
- `tests/openMob.Tests/Services/ActiveProjectServiceTests.cs` — 5 new tests

## Test Coverage

24 new tests added (14 AppStateService + 5 SplashViewModel + 5 ActiveProjectService). All 967 tests pass.

## Review Outcome

**Verdict: Approved with remarks** (0 Critical, 0 Major, 4 Minor — all resolved)
