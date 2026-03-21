# Onboarding Wizard — Replace Provider Step with Default Model Selection

## Metadata
| Field       | Value                                                |
|-------------|------------------------------------------------------|
| Date        | 2026-03-21                                           |
| Status      | **Completed**                                        |
| Version     | 1.0                                                  |
| Completed   | 2026-03-21                                           |
| Branch      | feature/onboarding-default-model (merged)            |

---

## Executive Summary

The onboarding wizard was refactored from 5 steps to 4 steps. The provider setup step was removed (provider configuration is the server's responsibility). A new "Default Model Selection" step was added after the server connection step. The user must select a default AI model before completing the wizard. The selected model is persisted as `DefaultModelId` on the `ServerConnection` entity in SQLite. The `ServerDetailPage` was also extended to display and change the default model.

---

## Key Decisions

1. **Provider step removed** — AI provider configuration is the server's responsibility, not the mobile app's. The wizard no longer asks the user to configure providers.

2. **Model selection is mandatory** — The wizard cannot be completed without selecting a model. If model loading fails, the step blocks advancement (no skip).

3. **DefaultModelId on ServerConnection entity** — The default model is a per-server setting stored locally in SQLite. It is NOT synced to the server. The relationship between `Server.DefaultModelId` and `ProjectPreference.DefaultModelId` is deferred to a future spec.

4. **Reuse of IProviderService for model fetching** — Models are extracted from `IProviderService.GetConfiguredProvidersAsync()` using the same `ExtractModelsFromProvider` pattern established by `ModelPickerViewModel`. No new API endpoints needed.

5. **Safe callback pattern in ServerDetailViewModel** — The `ChangeDefaultModelCommand` uses a `SafeSetDefaultModelAsync` fire-and-forget with try/catch instead of an async void lambda, preventing unobserved exceptions.

---

## Files Created

- `src/openMob.Core/Data/Migrations/20260321010000_AddDefaultModelIdToServerConnections.cs` — Migration adding `DefaultModelId TEXT NULL`
- `src/openMob/Views/Controls/OnboardingModelSelectionView.xaml` + `.xaml.cs` — New wizard step UI

## Files Modified

- `src/openMob.Core/Data/Entities/ServerConnection.cs` — Added `DefaultModelId` property
- `src/openMob.Core/Data/AppDbContext.cs` — Configured `DefaultModelId` column
- `src/openMob.Core/Infrastructure/Dtos/ServerConnectionDto.cs` — Added `DefaultModelId` parameter
- `src/openMob.Core/Data/Repositories/IServerConnectionRepository.cs` — Added `GetDefaultModelAsync`, `SetDefaultModelAsync`
- `src/openMob.Core/Data/Repositories/ServerConnectionRepository.cs` — Implemented new methods + updated `MapToDto`
- `src/openMob.Core/ViewModels/OnboardingViewModel.cs` — Major refactor: 5→4 steps, model selection logic
- `src/openMob.Core/ViewModels/ServerDetailViewModel.cs` — Default model display and change
- `src/openMob/Views/Pages/OnboardingPage.xaml` — Updated to 4-step flow
- `src/openMob/Views/Pages/ServerDetailPage.xaml` — Added default model section

## Files Deprecated

- `src/openMob/Views/Controls/OnboardingProviderSetupView.xaml` — Replaced by OnboardingModelSelectionView
- `src/openMob/Views/Controls/OnboardingPermissionsView.xaml` — Removed from wizard flow

## Test Coverage

27 new tests (15 OnboardingViewModel + 5 ServerDetailViewModel + 7 ServerConnectionRepository). All 971 tests pass.

## Review Outcome

**Initial verdict: Changes required** (1 Critical, 4 Major). All resolved:
- [C-001] Fixed async void callback → SafeSetDefaultModelAsync pattern
- [M-001] XAML updated for 4-step flow (om-mobile-ui)
- [M-002] ServerDetailPage XAML updated with default model section
- [M-003] Guard added for skip-step-2 scenario (no server connected)
- [M-004] URL sanitized before Sentry logging
