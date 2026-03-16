# ADR: Shell Push Navigation via Routing.RegisterRoute

## Date
2026-03-16

## Status
Accepted

## Context
New pages that should be pushed onto the navigation stack (not replace the root) need to be navigable via `INavigationService.GoToAsync("route-name")` using relative routes. The initial implementation of `ServerManagementPage` and `ServerDetailPage` registered them as `<ShellContent>` in `AppShell.xaml`, which caused a runtime crash: "Relative routing to shell elements is currently not supported. Try prefixing your uri with ///".

## Decision
All push-navigation pages must be registered via `Routing.RegisterRoute(routeName, typeof(PageType))` in `AppShell.xaml.cs`, not as `<ShellContent>` in `AppShell.xaml`.

`<ShellContent>` is reserved for root-level Shell pages (splash, onboarding, chat) that are navigated to with absolute routes (`///route`). Push pages (settings, project-detail, server-management, server-detail) use `Routing.RegisterRoute`.

## Rationale
- `<ShellContent>` entries become part of the Shell hierarchy and require absolute navigation (`///`).
- `Routing.RegisterRoute` registers pages as detached routes that support relative push navigation.
- This is the established pattern already used by `ProjectDetailPage`, `SettingsPage`, `ProjectsPage`, and all popup sheets.

## Alternatives Considered
- **Use `///server-management` absolute navigation**: Would work but breaks the back-navigation stack and is semantically wrong for a settings sub-page.
- **Keep as ShellContent with FlyoutItemIsVisible=False**: Causes the crash described above when using relative routes.

## Consequences
### Positive
- Consistent with all other push-navigation pages in the project.
- Back button works correctly (pops back to Settings).
- No changes to `INavigationService` interface required.

### Negative / Trade-offs
- Pages registered via `Routing.RegisterRoute` are not visible in the XAML Shell hierarchy — developers must check `AppShell.xaml.cs` to see all registered routes.

## Related Features
server-management-ui

## Related Agents
om-mobile-ui (registers routes in AppShell), om-mobile-core (uses INavigationService with route names)
