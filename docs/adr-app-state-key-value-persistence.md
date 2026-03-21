# ADR: AppState Key-Value Table for Global App Preferences

## Date
2026-03-21

## Status
Accepted

## Context
The app needed to persist the last active project ID across app restarts. No global configuration table existed in SQLite — only `ServerConnections` and `ProjectPreferences` (which is per-project, not global). A mechanism was needed for storing simple global app state values.

## Decision
Created a new `AppState` table with a key-value schema (`Key TEXT PRIMARY KEY, Value TEXT NULL`). Exposed through `IAppStateService` registered as Singleton, using `IServiceScopeFactory` to resolve Scoped `AppDbContext` instances per operation.

## Rationale
A key-value table is the simplest approach for global app state that:
- May grow over time (future keys can be added without migrations)
- Has no complex relationships
- Requires only simple read/write operations
- Needs to be accessed from Singleton services

A dedicated entity per setting (e.g., a `UserPreferences` table with typed columns) would require a new migration for each new preference. The key-value approach is more flexible for an evolving mobile app.

## Alternatives Considered
- **MAUI `Preferences` API**: Platform-specific, not queryable via EF Core, not part of the SQLite database backup. Would fragment persistence across two stores. Rejected for consistency.
- **Column on `ServerConnections`**: `LastActiveProjectId` is not server-specific — it's a global app preference. Adding it to a server entity would be semantically wrong.
- **Separate `AppConfig` entity with typed columns**: Would require migrations for each new preference. Over-engineered for simple string values.

## Consequences
### Positive
- Single table for all future global app preferences (no new migrations needed for new keys)
- Consistent with the existing EF Core + SQLite persistence pattern
- Easily testable with in-memory SQLite

### Negative / Trade-offs
- No type safety on values (all stored as strings)
- No schema enforcement for required keys (a missing key returns null)
- Slight overhead from `IServiceScopeFactory` scope creation per operation

## Related Features
last-active-project-restore

## Related Agents
om-mobile-core (implementation), om-tester (testing)
