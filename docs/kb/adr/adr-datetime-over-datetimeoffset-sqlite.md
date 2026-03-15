# ADR: Use DateTime (UTC) Instead of DateTimeOffset for SQLite Entities

## Date
2026-03-15

## Status
Accepted

## Context
The `ServerConnection` entity initially used `DateTimeOffset` for `CreatedAt` and `UpdatedAt` fields. During code review, it was discovered that EF Core's SQLite provider does not natively support `DateTimeOffset` comparison and ordering operations. The `.OrderBy(sc => sc.CreatedAt)` query would cause client-side evaluation, defeating database-level sorting.

## Decision
Use `DateTime` (UTC) instead of `DateTimeOffset` for all timestamp fields in EF Core entities stored in SQLite. All timestamps are stored and compared as UTC. The `DateTime.UtcNow` is used consistently throughout.

## Rationale
- SQLite stores `DateTime` as TEXT in ISO 8601 format, which sorts correctly as strings
- EF Core can translate `OrderBy` on `DateTime` to SQL without client-side evaluation
- Microsoft's official guidance recommends `DateTime` over `DateTimeOffset` for SQLite
- Simpler — no need for custom `ValueConverter` or `ValueComparer`

## Alternatives Considered
- **Keep DateTimeOffset with HasConversion\<string\>()**: Rejected — EF Core still flags client evaluation for ordering, and the conversion adds complexity
- **Custom ValueConverter + ValueComparer**: Rejected — over-engineered for the use case; adds maintenance burden
- **Store as Unix timestamp (long)**: Rejected — loses human readability in the database

## Consequences
### Positive
- Database-level sorting works correctly
- No EF Core client evaluation warnings
- Simpler entity model

### Negative / Trade-offs
- Loses timezone offset information (acceptable since all timestamps are UTC)
- Spec REQ-001 still references `DateTimeOffset` — should be updated to `DateTime` in future revisions

## Related Features
opencode-server-connection-model

## Related Agents
om-mobile-core, om-tester, om-reviewer
