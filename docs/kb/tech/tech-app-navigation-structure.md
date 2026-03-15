# Technical Analysis — App Navigation Structure & Page Flow

**Feature slug:** app-navigation-structure
**Completed:** 2026-03-16
**Branch:** feature/app-navigation-structure
**Complexity:** High

---

## Change Classification

| Field | Value |
|-------|-------|
| Change type | Feature |
| Git Flow branch | feature/app-navigation-structure |
| Branches from | develop |
| Estimated complexity | High |
| Agents involved | om-mobile-core, om-mobile-ui, om-tester, om-reviewer |

## Technical Decisions

### 1. INavigationService Abstraction
First introduction of `INavigationService` in the project. Core interface in `openMob.Core.Services`, MAUI implementation (`MauiNavigationService`) wraps `Shell.Current`. All ViewModels use this interface — never `Shell.Current` directly. Enables full unit testing of navigation logic.

### 2. IAppPopupService Abstraction
Named `IAppPopupService` to avoid collision with UXDivers `IPopupService`. Core interface provides: `ShowConfirmDeleteAsync`, `ShowRenameAsync`, `ShowToastAsync`, `ShowErrorAsync`, `ShowOptionSheetAsync`, `PushPopupAsync`, `PopPopupAsync`. MAUI implementation (`MauiPopupService`) currently uses native alerts + CommunityToolkit.Maui Toast. UXDivers integration is a drop-in replacement.

### 3. Onboarding as Single Page with ContentView Swap
Single `OnboardingPage` with `ContentPresenter` that swaps child views based on `CurrentStep` (1-5). Uses `StepToVisibilityConverter` to show/hide step ContentViews. Avoids Shell navigation animations between steps. ProgressBar stays persistent.

### 4. SplashPage as Non-Flyout ShellContent
`ShellContent` with `FlyoutItemIsVisible="False"` and `Shell.NavBarIsVisible="False"`. `SplashViewModel.InitializeCommand` runs on appearing and navigates away using `//route` absolute routes to prevent back navigation.

### 5. Service Layer Wrapping IOpencodeApiClient
Thin service interfaces (`IProjectService`, `ISessionService`, `IAgentService`, `IProviderService`) provide cleaner API surface. Handle `OpencodeResult<T>` unwrapping, error mapping via Sentry, and `ConfigureAwait(false)`. Session filtering by project is client-side.

### 6. Shell Flyout with Custom Content
MAUI Shell `Shell.FlyoutContent`, `Shell.FlyoutHeader`, `Shell.FlyoutFooter` for fully custom flyout. `FlyoutViewModel` drives session list. All FlyoutItems hidden — navigation is programmatic.

### 7. ChatViewModel for Header Bar
Minimal ChatViewModel drives the header bar: ProjectName, SessionName, status banners, More Menu (6 actions via `IAppPopupService.ShowOptionSheetAsync`). Does NOT handle message list (future chat-ui-design-guidelines spec).

### 8. ProjectNameHelper Shared Utility
`ExtractFromWorktree(string path)` extracts last directory segment as display name. Used by ProjectsViewModel, ProjectDetailViewModel, ProjectSwitcherViewModel. Avoids code duplication.

## XAML Pitfall: StaticResource in Margin/Padding

**Critical lesson learned:** You CANNOT embed `{StaticResource}` markup extensions inside comma-separated `Margin` or `Padding` values in MAUI XAML. This causes `XamlParseException` at runtime.

```xml
<!-- INVALID — causes crash -->
Margin="0,{StaticResource SpacingSm},0,0"

<!-- VALID — use literal values -->
Margin="0,8,0,0"

<!-- VALID — single markup extension returning Thickness -->
Margin="{StaticResource SpacingLg}"
```

34 instances were found and fixed across 15 XAML files.

## Technical Risks Encountered

| Risk | Outcome |
|------|---------|
| UXDivers.Popups.Maui stability | Deferred — native alerts work as fallback |
| XAML StaticResource in Thickness | **Crashed at startup** — fixed by replacing with literals |
| Shell navigation absolute routes | Works correctly with `//route` syntax |
| Onboarding step 2 gating | Made skippable for dev/testing without server |

## API Endpoints Used

- `GET /global/health` — SplashViewModel bootstrap
- `GET /project`, `GET /project/current` — ProjectService
- `GET /session`, `POST /session`, `PUT /session/{id}`, `DELETE /session/{id}`, `POST /session/{id}/fork` — SessionService
- `GET /agent` — AgentService
- `GET /provider`, `POST /provider/{id}/auth` — ProviderService
