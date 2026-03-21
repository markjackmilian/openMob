# Technical Analysis — Onboarding Wizard: Default Model Selection
**Feature slug:** onboarding-wizard-default-model-step
**Completed:** 2026-03-21
**Branch:** feature/onboarding-default-model
**Complexity:** Medium

---

## Change Classification

| Field | Value |
|-------|-------|
| Change type | Feature |
| Git Flow branch | feature/onboarding-default-model |
| Branches from | develop |
| Estimated complexity | Medium |
| Estimated agents involved | om-mobile-core, om-mobile-ui, om-tester, om-reviewer |

## Layers Involved

| Layer | Agent | Scope |
|-------|-------|-------|
| Data / EF Core | om-mobile-core | ServerConnection entity, migration, AppDbContext |
| Repositories | om-mobile-core | IServerConnectionRepository, ServerConnectionRepository |
| ViewModels | om-mobile-core | OnboardingViewModel (major refactor), ServerDetailViewModel |
| XAML Views | om-mobile-ui | OnboardingPage, OnboardingModelSelectionView (new), ServerDetailPage |
| Unit Tests | om-tester | 27 new tests across 3 test files |

## Wizard Step Refactoring (5 → 4 Steps)

### Before
1. Welcome
2. Connect Server
3. Provider Setup ← **removed**
4. Permissions ← **removed**
5. Completion

### After
1. Welcome
2. Connect Server
3. **Default Model Selection** ← **new**
4. Completion

### Impact on ViewModel Properties
- `TotalSteps`: 5 → 4
- `Progress` calculation: `CurrentStep / 4.0` instead of `/ 5.0`
- `CanGoNext` step 3: requires `SelectedModelId != null && !IsLoadingModels && ModelLoadError == null`
- `IsStepOptional`: only step 2 (server connection can be skipped)
- Completion trigger: step 4 instead of step 5

## Model Loading Pattern

Models are loaded from the server via `IProviderService.GetConfiguredProvidersAsync()`, which calls `GET /config/providers`. Each `ProviderDto` contains a `Models` JsonElement. The `ExtractModelsFromProvider` method (same pattern as `ModelPickerViewModel`) parses this JSON to extract `ModelItem` records:

```csharp
foreach (var provider in providers)
{
    var models = ExtractModelsFromProvider(provider.Id, provider.Name, provider.Models);
    allModels.AddRange(models);
}
```

Each `ModelItem` contains: `Id` (format: `"providerId/modelId"`), `Name`, `ProviderName`, `ContextSize`, `IsSelected`.

## Database Schema Change

### ServerConnections Table (modified)
```sql
ALTER TABLE "ServerConnections" ADD COLUMN "DefaultModelId" TEXT NULL;
```

- Nullable — `NULL` means no default model set
- Max length: 500 characters
- Format: `"providerId/modelId"` (e.g., `"anthropic/claude-3-opus"`)

## Repository Extension

Two new methods on `IServerConnectionRepository`:
- `GetDefaultModelAsync(serverId, ct)` → reads `DefaultModelId` from entity
- `SetDefaultModelAsync(serverId, modelId, ct)` → updates `DefaultModelId` + `UpdatedAt` timestamp

## Safe Callback Pattern (ADR-worthy)

The `ServerDetailViewModel.ChangeDefaultModelCommand` opens a `ModelPickerSheet` via `IAppPopupService.ShowModelPickerAsync`. The popup uses an `Action<string>` callback (`OnModelSelected`). Since the callback needs to perform async work (DB save), a fire-and-forget pattern with error handling was used:

```csharp
onModelSelected: (modelId) =>
{
    _ = SafeSetDefaultModelAsync(modelId);
}

private async Task SafeSetDefaultModelAsync(string modelId)
{
    try
    {
        await _serverConnectionRepository.SetDefaultModelAsync(_savedServerId!, modelId);
        DefaultModelName = modelId;
    }
    catch (Exception ex)
    {
        SentryHelper.CaptureException(ex, ...);
    }
}
```

This avoids the async void anti-pattern while working within the `Action<string>` constraint of the existing popup API.

## Skip-Step-2 Guard

When step 2 (server connection) is skipped, `_savedConnectionId` is null. `LoadModelsAsync` now checks for this and sets `ModelLoadError = "Connect to a server first to load available models."`, which blocks advancement via `CanGoNext`.

## Technical Risks (Resolved)

- **Wizard step renumbering**: All step-dependent logic (Progress, CanGoNext, IsStepOptional, XAML visibility) updated and tested.
- **ServerConnectionDto is a record**: Added `DefaultModelId` as last parameter with `= null` default to minimize breaking changes.
- **URL sanitization**: `ServerUrl` is now sanitized via `Uri.GetLeftPart(UriPartial.Path)` before Sentry logging.
