# Technical Analysis — Last Active Project Restore on Startup
**Feature slug:** last-active-project-restore
**Completed:** 2026-03-21
**Branch:** feature/last-active-project-restore
**Complexity:** Medium

---

## Change Classification

| Field | Value |
|-------|-------|
| Change type | Feature |
| Git Flow branch | feature/last-active-project-restore |
| Branches from | develop |
| Estimated complexity | Medium |
| Estimated agents involved | om-mobile-core, om-tester, om-reviewer |

## Layers Involved

| Layer | Agent | Scope |
|-------|-------|-------|
| Data / EF Core | om-mobile-core | src/openMob.Core/Data/Entities/, src/openMob.Core/Data/AppDbContext.cs, src/openMob.Core/Data/Migrations/ |
| Business logic / Services | om-mobile-core | src/openMob.Core/Services/ |
| ViewModels | om-mobile-core | src/openMob.Core/ViewModels/SplashViewModel.cs, src/openMob.Core/Services/ActiveProjectService.cs |
| DI Registration | om-mobile-core | src/openMob.Core/Infrastructure/DI/CoreServiceExtensions.cs |
| Unit Tests | om-tester | tests/openMob.Tests/ |

## Architecture Pattern: Singleton Service with Scoped DbContext

`AppStateService` is registered as Singleton because app state is global. However, `AppDbContext` is registered as Scoped (default for EF Core). To avoid the captive dependency anti-pattern, `AppStateService` injects `IServiceScopeFactory` and creates a new scope for each database operation:

```csharp
internal sealed class AppStateService : IAppStateService
{
    private readonly IServiceScopeFactory _scopeFactory;

    public async Task<string?> GetLastActiveProjectIdAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var state = await db.AppStates.FirstOrDefaultAsync(x => x.Key == Key, ct).ConfigureAwait(false);
        return state?.Value;
    }
}
```

This pattern is established in the codebase and should be reused for any future Singleton service that needs database access.

## Architecture Pattern: Persistence at Service Layer (not ViewModel)

Project changes happen in multiple ViewModels. Instead of adding `IAppStateService` calls to each ViewModel, persistence was integrated into `ActiveProjectService.SetActiveProjectAsync` — the single entry point for all project changes. This ensures:
- No ViewModel modification needed
- All change points automatically covered
- Single responsibility: `ActiveProjectService` owns the active project concept end-to-end

## SplashViewModel Startup Flow (Updated)

```
1. Check server configured → if not → //onboarding
2. Check server reachable → if not → //server-management (after 2s delay)
3. ★ NEW: Restore active project from SQLite
   a. Read LastActiveProjectId from AppState
   b. If found & project exists → SetActiveProjectAsync(id)
   c. If not found or project gone → fallback to first project
   d. If no projects → do nothing (unchanged behavior)
   e. If any error → swallow + Sentry, continue
4. Check sessions → navigate to //chat (with or without sessionId)
```

## Database Schema

### AppState Table (new)
```sql
CREATE TABLE "AppStates" (
    "Key" TEXT NOT NULL CONSTRAINT "PK_AppStates" PRIMARY KEY,
    "Value" TEXT NULL
);
```

Single row used: `Key = "LastActiveProjectId"`, `Value = "<project-id-string>"`.

## Technical Risks (Resolved)

- **Singleton + Scoped DbContext**: Resolved with `IServiceScopeFactory` pattern.
- **Race condition on startup**: Not an issue — `InitializeAsync` is sequential.
- **Migration on existing databases**: Additive — new table, no existing data affected.

## Test Strategy

- `AppStateServiceTests`: Real in-memory SQLite via `TestDbContextFactory` — tests actual EF Core behavior
- `SplashViewModelTests`: NSubstitute mocks for all services — tests routing logic
- `ActiveProjectServiceTests`: NSubstitute mocks — tests persistence integration
