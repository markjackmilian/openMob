# ADR: Service Layer Wrapping IOpencodeApiClient

## Date
2026-03-16

## Status
Accepted

## Context
ViewModels need to call opencode server API operations (projects, sessions, agents, providers). The existing `IOpencodeApiClient` returns `OpencodeResult<T>` which requires error checking, unwrapping, and Sentry logging at every call site. Having ViewModels call `IOpencodeApiClient` directly would duplicate error handling logic and expose HTTP-level concerns to the ViewModel layer.

## Decision
Introduce thin service interfaces that wrap `IOpencodeApiClient`:
- `IProjectService` / `ProjectService` — project operations
- `ISessionService` / `SessionService` — session operations (including client-side project filtering)
- `IAgentService` / `AgentService` — agent listing
- `IProviderService` / `ProviderService` — provider operations and auth

Services handle `OpencodeResult<T>` unwrapping, Sentry error logging, and return clean types (lists, nullable objects, booleans). Services use `ConfigureAwait(false)` on all async calls.

## Rationale
- Single responsibility: ViewModels handle UI state, services handle API interaction
- Error handling centralized in service layer (Sentry logging, result unwrapping)
- Enables client-side filtering (e.g., `GetSessionsByProjectAsync` filters by ProjectId)
- Testable: ViewModels mock service interfaces, service tests mock `IOpencodeApiClient`
- Follows existing pattern: `IServerConnectionRepository` wraps EF Core operations

## Alternatives Considered
- **ViewModels call IOpencodeApiClient directly**: Simpler but duplicates error handling across 10+ ViewModels.
- **MediatR / CQRS pattern**: Over-engineered for the current scope. Services are sufficient.

## Consequences
### Positive
- Clean ViewModel code — no `OpencodeResult<T>` handling in ViewModels
- Centralized error logging
- Easy to add caching later (in service layer)

### Negative / Trade-offs
- Extra layer of abstraction (4 service interfaces + 4 implementations)
- Session project filtering is client-side (fetches all sessions, filters in memory)

## Related Features
app-navigation-structure

## Related Agents
om-mobile-core
