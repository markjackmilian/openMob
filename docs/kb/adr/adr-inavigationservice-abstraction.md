# ADR: INavigationService Abstraction for Shell Navigation

## Date
2026-03-16

## Status
Accepted

## Context
ViewModels need to perform navigation (e.g., SplashViewModel routing to onboarding or chat, ProjectsViewModel navigating to project detail). Direct use of `Shell.Current.GoToAsync()` in ViewModels creates a hard dependency on MAUI Shell, making ViewModels untestable and violating the layer separation rule (openMob.Core has zero MAUI dependencies).

## Decision
Introduce `INavigationService` as a Core interface in `openMob.Core.Services` with three methods:
- `GoToAsync(string route, CancellationToken ct)`
- `GoToAsync(string route, IDictionary<string, object> parameters, CancellationToken ct)`
- `PopAsync(CancellationToken ct)`

The MAUI project provides `MauiNavigationService` which wraps `Shell.Current`. Registered as Singleton in `MauiProgram.cs`.

## Rationale
- Enables unit testing of all navigation logic with NSubstitute mocks
- Maintains openMob.Core as a pure .NET library with zero MAUI dependencies
- Follows the existing pattern of platform abstractions (IServerCredentialStore)
- All 10 ViewModels in this feature use INavigationService — consistent pattern

## Alternatives Considered
- **MessagingCenter / WeakReferenceMessenger**: Would decouple navigation but adds complexity and makes navigation flow harder to trace. Navigation is a direct action, not an event.
- **Shell.Current directly in ViewModels**: Simpler but untestable and violates layer separation.

## Consequences
### Positive
- All ViewModel navigation logic is fully unit-testable
- Clean separation between Core and MAUI layers
- Consistent pattern for all future ViewModels

### Negative / Trade-offs
- One extra layer of indirection for navigation calls
- MauiNavigationService must be kept in sync with Shell route definitions

## Related Features
app-navigation-structure

## Related Agents
om-mobile-core, om-mobile-ui
