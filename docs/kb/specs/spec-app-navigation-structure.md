# Spec: App Navigation Structure & Page Flow

## Metadata
| Field       | Value                                          |
|-------------|------------------------------------------------|
| Date        | 2026-03-15                                     |
| Status      | **Completed**                                  |
| Version     | 1.1                                            |
| Completed   | 2026-03-16                                     |
| Branch      | feature/app-navigation-structure (merged)      |
| Merged into | develop                                        |

## Summary

Complete navigation structure for openMob: **Splash → Onboarding → Chat (root screen)**. Chat-first approach — no Home page. Context (project, agent, model) visible in Chat header. Navigation via Shell Flyout and bottom sheet popups.

## Key Design Decisions

- **D-001:** Chat is the root screen (no Home page)
- **D-002:** Sessions accessible in Flyout, not a separate page
- **D-003:** Project switching via bottom sheet from Chat header
- **D-004:** Agent/Model selection via bottom sheet popups
- **D-005:** Onboarding steps 2 and 3 are skippable
- **D-006:** UXDivers.Popups.Maui for all dialogs (deferred — IAppPopupService abstraction in place)

## Pages Implemented

| Page | Route | Purpose |
|------|-------|---------|
| SplashPage | `splash` | Bootstrap routing (4 scenarios) |
| OnboardingPage | `onboarding` | 5-step setup (Welcome, Server, Provider, Permissions, Completion) |
| ChatPage | `chat` | Root screen with contextual header bar |
| ProjectsPage | `projects` | Project list with Active badge |
| ProjectDetailPage | `project-detail` | Project info, sessions, agent/model defaults |
| SettingsPage | `settings` | Stub (future spec) |

## Popup Sheets

| Sheet | Trigger | Purpose |
|-------|---------|---------|
| ProjectSwitcherSheet | Tap ProjectName in Chat header | Quick project switch |
| AgentPickerSheet | More Menu / ProjectDetail | Agent selection |
| ModelPickerSheet | More Menu / ProjectDetail | Model selection (grouped by provider) |
| AddProjectSheet | "+" button on ProjectsPage | New project creation |

## Architecture

- **INavigationService** — Core abstraction over Shell.Current (first introduction)
- **IAppPopupService** — Core abstraction over popup/dialog operations
- **Service layer** — IProjectService, ISessionService, IAgentService, IProviderService wrap IOpencodeApiClient
- **10 ViewModels** — SplashViewModel, OnboardingViewModel, ChatViewModel, FlyoutViewModel, ProjectsViewModel, ProjectDetailViewModel, AddProjectViewModel, ProjectSwitcherViewModel, AgentPickerViewModel, ModelPickerViewModel
- **260 unit tests** — all passing

## Known Deviations

- **UXDivers.Popups.Maui** (REQ-033–041): Deferred to follow-up spec. Current implementation uses native MAUI alerts + CommunityToolkit.Maui Toast. The `IAppPopupService` abstraction enables drop-in replacement.
- **Converters** live in `openMob/Converters/` (MAUI project) instead of `openMob.Core` — not testable from test project.

## Files Created

- 10 service files in `src/openMob.Core/Services/`
- 10 ViewModel files in `src/openMob.Core/ViewModels/`
- 7 model files in `src/openMob.Core/Models/`
- 1 helper in `src/openMob.Core/Helpers/`
- 6 XAML pages in `src/openMob/Views/Pages/`
- 4 XAML popups in `src/openMob/Views/Popups/`
- 9 XAML controls in `src/openMob/Views/Controls/`
- 5 converters in `src/openMob/Converters/`
- 2 platform services in `src/openMob/Services/`
- 12 test files in `tests/openMob.Tests/`
