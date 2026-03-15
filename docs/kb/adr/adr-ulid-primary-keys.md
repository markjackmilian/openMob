# ADR: Use ULID for Entity Primary Keys

## Date
2026-03-15

## Status
Accepted

## Context
The `ServerConnection` entity needed a primary key strategy. The spec offered ULID or GUID as options. The key needed to be a string type (compatible with SQLite TEXT), unique, and ideally sortable by creation time for list views.

## Decision
Use ULID (Universally Unique Lexicographically Sortable Identifier) via the `Ulid` NuGet package. Primary keys are generated as `Ulid.NewUlid().ToString()` and stored as TEXT in SQLite.

## Rationale
- ULIDs are lexicographically sortable by creation time — natural ordering in list views without an extra `ORDER BY CreatedAt`
- 128-bit, globally unique — same collision resistance as UUIDs
- String representation is URL-safe and compact (26 characters vs 36 for GUID)
- The `Ulid` NuGet package is lightweight with no transitive dependencies

## Alternatives Considered
- **GUID (Guid.NewGuid().ToString())**: Rejected — not time-sortable, random ordering in lists
- **Auto-increment integer**: Rejected — not suitable for distributed/offline scenarios, reveals record count
- **Snowflake IDs**: Rejected — requires a central coordinator, over-engineered for mobile app

## Consequences
### Positive
- Natural time-ordering for list views
- Globally unique without coordination
- Compact string representation

### Negative / Trade-offs
- Adds `Ulid` NuGet package dependency to `openMob.Core`
- Slightly larger than integer keys (26 chars vs 4-8 bytes)

## Related Features
opencode-server-connection-model

## Related Agents
om-mobile-core
