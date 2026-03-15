# ADR: IAppPopupService Abstraction for Popup/Dialog Operations

## Date
2026-03-16

## Status
Accepted

## Context
The app needs to show confirmation dialogs, rename prompts, option sheets, toasts, and error dialogs from ViewModels. The spec mandates UXDivers.Popups.Maui as the popup library (REQ-033–041), but ViewModels in openMob.Core cannot reference MAUI packages. Additionally, the popup library choice may change, and ViewModels should not be coupled to any specific implementation.

## Decision
Introduce `IAppPopupService` as a Core interface in `openMob.Core.Services` with methods:
- `ShowConfirmDeleteAsync(title, message)` → `bool`
- `ShowRenameAsync(currentName)` → `string?`
- `ShowToastAsync(message)`
- `ShowErrorAsync(title, message)`
- `ShowOptionSheetAsync(title, options)` → `string?`
- `PushPopupAsync(popup)` / `PopPopupAsync()`

Named `IAppPopupService` (not `IPopupService`) to avoid collision with UXDivers' own `IPopupService`.

The MAUI project provides `MauiPopupService`. Current implementation uses native MAUI alerts + CommunityToolkit.Maui Toast. UXDivers integration is planned as a follow-up — only `MauiPopupService` needs to change.

## Rationale
- Decouples ViewModels from any specific popup library
- Enables unit testing of all dialog interactions (confirm/cancel paths)
- UXDivers integration becomes a single-file change (MauiPopupService.cs)
- REQ-036 explicitly requires DI injection, not static `IPopupService.Current`

## Alternatives Considered
- **Direct UXDivers IPopupService injection**: Would couple Core to UXDivers package. Rejected.
- **MessagingCenter for dialog requests**: Over-engineered for simple request/response dialogs.
- **Native DisplayAlert/DisplayActionSheet**: Not abstractable, not testable, violates REQ-033.

## Consequences
### Positive
- All dialog logic in ViewModels is fully unit-testable
- Popup library can be swapped without touching any ViewModel
- Consistent API for all popup types

### Negative / Trade-offs
- `PushPopupAsync(object popup)` uses `object` parameter — loses type safety for custom popups
- UXDivers integration deferred — current implementation uses native alerts (functional but not spec-compliant for REQ-033–041)

## Related Features
app-navigation-structure

## Related Agents
om-mobile-core, om-mobile-ui
