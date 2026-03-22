# UXDivers Popups Migration

## Metadata
| Field   | Value                        |
|---------|------------------------------|
| Date    | 2026-03-22                   |
| Status  | In Progress                  |
| Version | 1.0                          |

---

## Executive Summary

Il progetto adotta **UXDivers Popups** (`UXDivers.Popups.Maui`) come libreria unica per tutti i popup dell'app, sostituendo l'attuale mix di `Navigation.PushModalAsync` (ContentPage modal), `DisplayAlert`, `DisplayActionSheet` e il placeholder no-op `PushPopupAsync`. La migrazione converte i 6 popup esistenti da `ContentPage` a `PopupPage`, aggiorna `MauiPopupService` per usare `IPopupService.Current`, e risolve il crash del ContextSheet causato dall'incoerenza tra push (no-op) e pop (PopModalAsync su stack vuoto). Vengono inoltre aggiunti i due metodi mancanti `ShowProjectSwitcherAsync` e `ShowAddProjectAsync` all'interfaccia `IAppPopupService`.

---

## Scope

### In Scope
- Installazione NuGet `UXDivers.Popups.Maui` (v0.9.4, ultima stabile) nel progetto `src/openMob/`.
- Configurazione `UseUXDiversPopups()` in `MauiProgram.cs` e aggiunta `DarkTheme` + `PopupStyles` in `App.xaml`.
- Conversione dei 6 popup esistenti da `ContentPage` a `PopupPage`: `ContextSheet`, `ModelPickerSheet`, `AgentPickerSheet`, `CommandPaletteSheet`, `ProjectSwitcherSheet`, `AddProjectSheet`.
- Registrazione dei 6 popup con `AddTransientPopup<TPopup, TViewModel>()` in `MauiProgram.cs` (sostituisce i `AddTransient<>` plain attuali).
- Refactoring completo di `MauiPopupService`: tutti i metodi `Show*Async` usano `IPopupService.Current.PushAsync(popup)` e `PopPopupAsync` usa `IPopupService.Current.PopAsync()`.
- Migrazione di `ShowConfirmDeleteAsync`, `ShowRenameAsync`, `ShowOptionSheetAsync` da `DisplayAlert`/`DisplayActionSheet` nativi a popup UXDivers (`SimpleActionPopup`, `FormPopup`, `OptionSheetPopup`).
- Aggiunta di `ShowProjectSwitcherAsync` e `ShowAddProjectAsync` a `IAppPopupService` e implementazione in `MauiPopupService`.
- Aggiornamento di `ChatViewModel.OpenProjectSwitcherAsync` (attualmente no-op) e `ProjectsViewModel.ShowAddProjectAsync` (attualmente toast placeholder) per delegare ai nuovi metodi `IAppPopupService`.
- Rimozione delle route Shell per i popup da `AppShell.xaml.cs` (`project-switcher`, `agent-picker`, `model-picker`, `add-project`) — non più necessarie con UXDivers.
- Fix del crash del ContextSheet come conseguenza diretta della migrazione.

### Out of Scope
- Theming custom dei popup con i design token dell'app (spec separata futura).
- Animazioni custom sui popup.
- Introduzione di nuovi popup non ancora esistenti nel codebase.
- Modifiche alla logica di business dei ViewModel Core (eccetto il wiring dei due comandi placeholder verso `IAppPopupService`).
- Supporto Windows/macOS (target: iOS e Android).

---

## Functional Requirements

> Requirements are numbered for traceability.

1. **[REQ-001]** Il progetto `src/openMob/openMob.csproj` deve referenziare il pacchetto NuGet `UXDivers.Popups.Maui` versione `0.9.4` (o superiore compatibile).

2. **[REQ-002]** In `MauiProgram.cs`, il builder deve chiamare `.UseUXDiversPopups()` nella catena di configurazione, dopo `.UseMauiCommunityToolkit()`.

3. **[REQ-003]** In `App.xaml`, le `ResourceDictionary.MergedDictionaries` devono includere `<uxd:DarkTheme />` e `<uxd:PopupStyles />` (namespace `xmlns:uxd="clr-namespace:UXDivers.Popups.Maui.Controls;assembly=UXDivers.Popups.Maui"`).

4. **[REQ-004]** Tutti e 6 i popup (`ContextSheet`, `ModelPickerSheet`, `AgentPickerSheet`, `CommandPaletteSheet`, `ProjectSwitcherSheet`, `AddProjectSheet`) devono avere come base class `PopupPage` (XAML root tag `<uxd:PopupPage>` e code-behind `: PopupPage`), non più `ContentPage`.

5. **[REQ-005]** In `MauiProgram.cs`, i 6 popup devono essere registrati con `AddTransientPopup<TPopup, TViewModel>()` (UXDivers DI extension), sostituendo i `builder.Services.AddTransient<TPopup>()` plain attuali. I ViewModel associati sono: `ContextSheet` ↔ `ContextSheetViewModel`, `ModelPickerSheet` ↔ `ModelPickerViewModel`, `AgentPickerSheet` ↔ `AgentPickerViewModel`, `CommandPaletteSheet` ↔ `CommandPaletteViewModel`, `ProjectSwitcherSheet` ↔ `ProjectSwitcherViewModel`, `AddProjectSheet` ↔ `AddProjectViewModel`.

6. **[REQ-006]** Tutti i metodi `Show*Async` in `MauiPopupService` che presentano un popup custom (ContextSheet, ModelPickerSheet, AgentPickerSheet, CommandPaletteSheet, ProjectSwitcherSheet, AddProjectSheet) devono usare `IPopupService.Current.PushAsync(popup)` al posto di `Navigation.PushModalAsync(sheet, animated: true)`.

7. **[REQ-007]** `MauiPopupService.PopPopupAsync` deve usare `IPopupService.Current.PopAsync()` (chiude il topmost popup) al posto di `navigation.PopModalAsync(animated: true)`. Il controllo `ModalStack.Count > 0` deve essere rimosso.

8. **[REQ-008]** `ShowContextSheetAsync` deve mantenere il pattern "initialize before push": `vm.InitializeAsync(projectId, sessionId, ct)` viene chiamato prima di `IPopupService.Current.PushAsync(popup)`, garantendo che il sheet sia popolato al momento dell'apertura.

9. **[REQ-009]** `ShowConfirmDeleteAsync` deve essere reimplementato usando `SimpleActionPopup` di UXDivers con due bottoni (testo configurabile per conferma e annulla). Il metodo deve restituire `true` se l'utente preme il bottone di conferma, `false` altrimenti (incluso dismiss via backdrop).

10. **[REQ-010]** `ShowRenameAsync` deve essere reimplementato usando `FormPopup` di UXDivers con un singolo campo testo pre-compilato con `currentName`. Il metodo deve restituire il testo inserito se confermato, `null` se annullato o dismissed.

11. **[REQ-011]** `ShowOptionSheetAsync` deve essere reimplementato usando `OptionSheetPopup` di UXDivers con le opzioni fornite. Il metodo deve restituire la stringa dell'opzione selezionata, `null` se dismissed.

12. **[REQ-012]** `IAppPopupService` deve esporre un nuovo metodo `Task ShowProjectSwitcherAsync(CancellationToken ct = default)` con documentazione XML `<summary>`.

13. **[REQ-013]** `IAppPopupService` deve esporre un nuovo metodo `Task ShowAddProjectAsync(CancellationToken ct = default)` con documentazione XML `<summary>`.

14. **[REQ-014]** `MauiPopupService` deve implementare `ShowProjectSwitcherAsync`: risolve `ProjectSwitcherSheet` e la presenta con `IPopupService.Current.PushAsync(popup)`. Il caricamento dei progetti (attualmente in `OnAppearing`) deve avvenire prima del push, seguendo il pattern "initialize before push" già stabilito per `ShowContextSheetAsync`.

15. **[REQ-015]** `MauiPopupService` deve implementare `ShowAddProjectAsync`: risolve `AddProjectSheet` e la presenta con `IPopupService.Current.PushAsync(popup)`.

16. **[REQ-016]** `ChatViewModel.OpenProjectSwitcherAsync` deve delegare a `_popupService.ShowProjectSwitcherAsync(ct)`, rimuovendo il no-op `await Task.CompletedTask` attuale. `IAppPopupService` deve essere aggiunto come dipendenza di `ChatViewModel` se non già presente.

17. **[REQ-017]** `ProjectsViewModel.ShowAddProjectAsync` deve delegare a `_popupService.ShowAddProjectAsync(ct)`, rimuovendo il toast placeholder `await _popupService.ShowToastAsync(...)` attuale.

18. **[REQ-018]** Le route Shell per i popup devono essere rimosse da `AppShell.xaml.cs`: `Routing.RegisterRoute("project-switcher", ...)`, `Routing.RegisterRoute("agent-picker", ...)`, `Routing.RegisterRoute("model-picker", ...)`, `Routing.RegisterRoute("add-project", ...)`.

19. **[REQ-019]** `MauiPopupService` non deve più dipendere da `IServiceProvider` per risolvere manualmente i popup tramite `GetRequiredService<T>()`. La risoluzione avviene tramite `IPopupService.Current.PushAsync<TPopup>()` (by-type, DI-backed) oppure per istanza pre-configurata.

20. **[REQ-020]** La build dell'intera solution (`dotnet build openMob.sln`) deve completare con exit code 0 e zero warning dopo la migrazione.

---

## Functional Impacts

### Affected Components / Systems
| Component | Impact | Notes |
|-----------|--------|-------|
| `src/openMob/openMob.csproj` | Aggiunta dipendenza NuGet | `UXDivers.Popups.Maui` v0.9.4 |
| `src/openMob/MauiProgram.cs` | `UseUXDiversPopups()` + `AddTransientPopup<>` per 6 popup | Rimuovere `AddTransient<>` plain per i popup |
| `src/openMob/App.xaml` | Aggiunta `DarkTheme` + `PopupStyles` nelle MergedDictionaries | Namespace `uxd:` da aggiungere |
| `src/openMob/AppShell.xaml.cs` | Rimozione 4 route popup | `project-switcher`, `agent-picker`, `model-picker`, `add-project` |
| `src/openMob/Services/MauiPopupService.cs` | Refactoring completo | Tutti i metodi Show*/Pop* + 2 nuovi metodi |
| `src/openMob.Core/Services/IAppPopupService.cs` | +2 metodi | `ShowProjectSwitcherAsync`, `ShowAddProjectAsync` |
| `src/openMob/Views/Popups/ContextSheet.xaml` + `.cs` | Base class `ContentPage` → `PopupPage` | |
| `src/openMob/Views/Popups/ModelPickerSheet.xaml` + `.cs` | Base class `ContentPage` → `PopupPage` | |
| `src/openMob/Views/Popups/AgentPickerSheet.xaml` + `.cs` | Base class `ContentPage` → `PopupPage` | |
| `src/openMob/Views/Popups/CommandPaletteSheet.xaml` + `.cs` | Base class `ContentPage` → `PopupPage` | |
| `src/openMob/Views/Popups/ProjectSwitcherSheet.xaml` + `.cs` | Base class `ContentPage` → `PopupPage` | `OnAppearing` → initialize before push |
| `src/openMob/Views/Popups/AddProjectSheet.xaml` + `.cs` | Base class `ContentPage` → `PopupPage` | |
| `src/openMob.Core/ViewModels/ChatViewModel.cs` | `OpenProjectSwitcherAsync` delega a `IAppPopupService` | `IAppPopupService` already injected |
| `src/openMob.Core/ViewModels/ProjectsViewModel.cs` | `ShowAddProjectAsync` delega a `IAppPopupService` | Rimuovere toast placeholder |

### Dependencies
- `UXDivers.Popups.Maui` v0.9.4 — Apache License 2.0, disponibile su NuGet.org, supports net10.0
- `CommunityToolkit.Maui` già presente — nessun conflitto noto con UXDivers Popups
- `IAppPopupService` (già in `openMob.Core`) — interfaccia estesa, non sostituita
- Pattern "initialize before push" già stabilito in `session-context-sheet-1of3-core` per `ShowContextSheetAsync` — da applicare uniformemente a `ShowProjectSwitcherAsync`

---

## Open Questions & Clarifications

| # | Question | Status | Answer / Decision |
|---|----------|--------|-------------------|
| 1 | `SimpleActionPopup` e `FormPopup` di UXDivers implementano `PopupResultPage<T>` per restituire un risultato tipizzato? | Resolved | **Yes.** UXDivers documentation confirms: `FormPopup` returns `List<string?>` via `PopupResultPage<List<string?>>`. `SimpleActionPopup` does NOT return a typed result — it extends `PopupPage`, not `PopupResultPage<T>`. For `ShowConfirmDeleteAsync`, use `PushAsync(popup)` with `waitUntilClosed: true` (default) and check which button was pressed via a callback or `TaskCompletionSource<bool>`. Alternatively, use a custom `PopupResultPage<bool>` wrapper. For `ShowRenameAsync`, `FormPopup` returns `List<string?>` — extract `result[0]`. For `ShowOptionSheetAsync`, `OptionSheetPopup` returns the selected option string. |
| 2 | `ProjectSwitcherSheet.OnAppearing` chiama `LoadProjectsCommand.ExecuteAsync(null)`. Con `PopupPage`, il lifecycle `OnAppearing` è ancora garantito? | Resolved | `PopupPage` does fire `OnAppearing` after the appearing animation. However, per the established "initialize before push" pattern, the loading should be moved to `MauiPopupService.ShowProjectSwitcherAsync` before `PushAsync`. Remove `OnAppearing` override from `ProjectSwitcherSheet`. |
| 3 | `MauiPopupService` è registrato come `Singleton` in `MauiProgram.cs`. Con UXDivers, `IPopupService.Current` è un singleton statico — nessun conflitto atteso. | Resolved | No conflict. `IPopupService.Current` is a static singleton managed by UXDivers. `MauiPopupService` as a Singleton accessing a static singleton is safe. Thread safety is handled by UXDivers internally (all popup operations must run on the UI thread, which is the standard MAUI constraint). |

---

## Acceptance Criteria

> Each criterion maps to one or more functional requirements.

- [ ] **[AC-001]** Given l'app avviata su Android/iOS, when si naviga normalmente, then non si verificano crash all'avvio né durante la navigazione. *(REQ-001, REQ-002, REQ-003)*

- [ ] **[AC-002]** Given il ContextSheet aperto, when l'utente preme "Delete session" e conferma, then il sheet si chiude senza crash e la sessione viene eliminata. *(REQ-006, REQ-007, REQ-008)* — **fix crash principale**

- [ ] **[AC-003]** Given un popup aperto tramite `IPopupService.Current.PushAsync`, when viene chiamato `PopPopupAsync`, then il popup si chiude correttamente. *(REQ-007)*

- [ ] **[AC-004]** Given `ShowConfirmDeleteAsync` invocato, when l'utente preme il bottone di conferma, then il metodo restituisce `true`; when preme annulla o chiude il popup, then restituisce `false`. *(REQ-009)*

- [ ] **[AC-005]** Given `ShowRenameAsync` invocato con `currentName = "foo"`, when l'utente modifica il testo e conferma, then il metodo restituisce il nuovo testo; when annulla, then restituisce `null`. *(REQ-010)*

- [ ] **[AC-006]** Given `ShowOptionSheetAsync` invocato con una lista di opzioni, when l'utente seleziona un'opzione, then il metodo restituisce quella stringa; when dismisses, then restituisce `null`. *(REQ-011)*

- [ ] **[AC-007]** Given la ChatPage, when l'utente preme il bottone di switch progetto nell'header, then il `ProjectSwitcherSheet` si apre come popup UXDivers con la lista dei progetti caricata. *(REQ-012, REQ-014, REQ-016)*

- [ ] **[AC-008]** Given la ProjectsPage, when l'utente preme il bottone "Add project", then l'`AddProjectSheet` si apre come popup UXDivers. *(REQ-013, REQ-015, REQ-017)*

- [ ] **[AC-009]** Given la build della solution, when si esegue `dotnet build openMob.sln`, then exit code 0 e zero warning. *(REQ-020)*

- [ ] **[AC-010]** Given tutti e 6 i popup, when aperti e chiusi in sequenza, then nessun leak di istanze (i popup Transient vengono ricreati ad ogni apertura). *(REQ-005)*

---

## Notes for Technical Analysis

> This section is addressed to the agent that will perform the technical implementation analysis.

### Key areas to investigate

1. **`SimpleActionPopup` e `FormPopup` come result popup** — Verified: `FormPopup` extends `PopupResultPage<List<string?>>` and returns form field values. `SimpleActionPopup` extends `PopupPage` (not `PopupResultPage<T>`). For `ShowConfirmDeleteAsync`, use `SimpleActionPopup` with `waitUntilClosed: true` and a `TaskCompletionSource<bool>` to capture the button press result. Alternatively, create a minimal custom `ConfirmDeletePopup : PopupResultPage<bool>`.

2. **`ProjectSwitcherSheet.OnAppearing` lifecycle** — Move `LoadProjectsCommand.ExecuteAsync` to `MauiPopupService.ShowProjectSwitcherAsync` before `PushAsync`. Remove `OnAppearing` override.

3. **`IServiceProvider` in `MauiPopupService`** — With `AddTransientPopup<TPopup, TViewModel>()`, UXDivers manages DI resolution internally when using `PushAsync<TPopup>()`. For popups requiring pre-push initialization (ContextSheet, ProjectSwitcherSheet, ModelPickerSheet, AgentPickerSheet), `IServiceProvider` is still needed to resolve the instance, configure the ViewModel, then push the instance. Keep `IServiceProvider` for these cases.

4. **Rimozione route Shell** — Routes `project-switcher`, `agent-picker`, `model-picker`, `add-project` in `AppShell.xaml.cs` must be removed. Verified: no ViewModel or View uses these routes via `INavigationService.GoToAsync(...)` — all popup presentation goes through `MauiPopupService`.

5. **`ChatViewModel` e `IAppPopupService`** — Verified: `ChatViewModel` already has `IAppPopupService` as `_popupService` dependency. `OpenProjectSwitcherAsync` can delegate directly without constructor changes.

6. **`MauiPopupService` as Singleton with `IPopupService.Current` static** — No conflict. Both are effectively singletons. Thread safety is guaranteed by MAUI's UI thread constraint.

### Suggested implementation approach

1. Add NuGet and configure `MauiProgram.cs` + `App.xaml` (REQ-001/002/003).
2. Convert 6 XAML + code-behind from `ContentPage` to `PopupPage` (REQ-004).
3. Update DI registrations with `AddTransientPopup<>` (REQ-005).
4. Update `IAppPopupService` with 2 new methods (REQ-012/013).
5. Refactor `MauiPopupService` method by method (REQ-006/007/008/009/010/011/014/015).
6. Update `ChatViewModel` and `ProjectsViewModel` (REQ-016/017).
7. Remove Shell routes (REQ-018).
8. Build + smoke test.

### Constraints to respect

- `openMob.Core` has **zero MAUI dependencies** — `IPopupService` from UXDivers must never be imported in `openMob.Core`. The `IAppPopupService` interface remains the only popup abstraction in Core.
- The name `IAppPopupService` was chosen explicitly to avoid collision with `IPopupService` from UXDivers. This convention must be maintained.
- All popups remain `Transient` — none should become `Singleton` without explicit justification.
- The "initialize before push" pattern must be applied uniformly to all popups requiring pre-loaded data.
- `async void` is forbidden except in MAUI lifecycle handlers (`OnAppearing`, etc.).

### Related files or modules

- `src/openMob/Services/MauiPopupService.cs` — main file to refactor
- `src/openMob.Core/Services/IAppPopupService.cs` — interface to extend
- `src/openMob/MauiProgram.cs` — DI composition root
- `src/openMob/App.xaml` — resource dictionaries
- `src/openMob/AppShell.xaml.cs` — route cleanup
- `src/openMob/Views/Popups/` — all 6 popups
- `src/openMob.Core/ViewModels/ChatViewModel.cs` — `OpenProjectSwitcherAsync`
- `src/openMob.Core/ViewModels/ProjectsViewModel.cs` — `ShowAddProjectAsync`

---

## Technical Analysis

> Added by: om-orchestrator | Date: 2026-03-22

### Change Classification

| Field | Value |
|-------|-------|
| Change type | Feature |
| Git Flow branch | feature/uxd-popups-migration |
| Branches from | develop |
| Estimated complexity | High |
| Estimated agents involved | om-mobile-core, om-mobile-ui, om-tester, om-reviewer |

### Layers Involved

| Layer | Agent | Scope |
|-------|-------|-------|
| Core Interface | om-mobile-core | `src/openMob.Core/Services/IAppPopupService.cs` |
| Core ViewModels | om-mobile-core | `ChatViewModel.cs`, `ProjectsViewModel.cs` |
| MAUI Service | om-mobile-core | `src/openMob/Services/MauiPopupService.cs` |
| DI / Config | om-mobile-core | `MauiProgram.cs`, `openMob.csproj` |
| XAML Popups | om-mobile-ui | `src/openMob/Views/Popups/` (6 popups) |
| App Resources | om-mobile-ui | `src/openMob/App.xaml` |
| Shell Routes | om-mobile-ui | `src/openMob/AppShell.xaml.cs` |
| Unit Tests | om-tester | `tests/openMob.Tests/` |
| Code Review | om-reviewer | all of the above |

### Files to Create

- None — this feature modifies existing files only. No new popup files are created.

### Files to Modify

- `src/openMob/openMob.csproj` — Add `<PackageReference Include="UXDivers.Popups.Maui" Version="0.9.4" />`
- `src/openMob/MauiProgram.cs` — Add `.UseUXDiversPopups()`, replace 6 `AddTransient<>` with `AddTransientPopup<TPopup, TViewModel>()`, remove `AddTransient<ProjectDetailPage>()` (if not already removed by project-list-one-tap-selection)
- `src/openMob/App.xaml` — Add `xmlns:uxd` namespace, add `<uxd:DarkTheme />` and `<uxd:PopupStyles />` to MergedDictionaries
- `src/openMob/AppShell.xaml.cs` — Remove 4 popup route registrations
- `src/openMob/Services/MauiPopupService.cs` — Complete refactoring: all Show*/Pop* methods use `IPopupService.Current`; add `ShowProjectSwitcherAsync` and `ShowAddProjectAsync`; keep `IServiceProvider` for pre-push initialization pattern
- `src/openMob.Core/Services/IAppPopupService.cs` — Add `ShowProjectSwitcherAsync` and `ShowAddProjectAsync` method signatures with XML docs
- `src/openMob/Views/Popups/ContextSheet.xaml` + `.cs` — Change base from `ContentPage` to `PopupPage`; update close to use `IPopupService.Current.PopAsync(this)`
- `src/openMob/Views/Popups/ModelPickerSheet.xaml` + `.cs` — Same migration; remove `OnAppearing` (loading handled by MauiPopupService)
- `src/openMob/Views/Popups/AgentPickerSheet.xaml` + `.cs` — Same migration; remove `OnAppearing`
- `src/openMob/Views/Popups/CommandPaletteSheet.xaml` + `.cs` — Same migration; remove `OnAppearing`
- `src/openMob/Views/Popups/ProjectSwitcherSheet.xaml` + `.cs` — Same migration; remove `OnAppearing` (loading moved to MauiPopupService)
- `src/openMob/Views/Popups/AddProjectSheet.xaml` + `.cs` — Same migration
- `src/openMob.Core/ViewModels/ChatViewModel.cs` — `OpenProjectSwitcherAsync` delegates to `_popupService.ShowProjectSwitcherAsync(ct)`
- `src/openMob.Core/ViewModels/ProjectsViewModel.cs` — `ShowAddProjectAsync` delegates to `_popupService.ShowAddProjectAsync(ct)`

### Technical Dependencies

- `UXDivers.Popups.Maui` v0.9.4 — new NuGet dependency (Apache 2.0 license, supports net10.0)
- `IPopupService.Current` — UXDivers static singleton for popup navigation
- `PopupPage` — UXDivers base class for custom popups (namespace `UXDivers.Popups.Maui`)
- `SimpleActionPopup` — UXDivers built-in for confirmation dialogs
- `FormPopup` — UXDivers built-in for form input, returns `List<string?>` via `PopupResultPage<List<string?>>`
- `OptionSheetPopup` — UXDivers built-in for option selection
- `AddTransientPopup<TPopup, TViewModel>()` — UXDivers DI extension method

### Technical Risks

- **`SimpleActionPopup` does not extend `PopupResultPage<T>`**: For `ShowConfirmDeleteAsync`, cannot use typed result. Must use `TaskCompletionSource<bool>` pattern or a custom `ConfirmDeletePopup : PopupResultPage<bool>`. Pragmatic approach: keep `DisplayAlert` for confirm/delete only, or use `SimpleActionPopup` with event callbacks.
- **XAML migration from `ContentPage` to `PopupPage`**: All `Shell.PresentationMode` attributes must be removed. All `Shell.Current.GoToAsync("..")` close calls must be replaced with `IPopupService.Current.PopAsync(this)`. The `SheetHandleBar` BoxView styling may need adjustment for the popup overlay context.
- **`OnAppearing` removal**: 4 popups use `OnAppearing` to trigger data loading. This must be moved to `MauiPopupService` pre-push initialization. If any popup's ViewModel doesn't expose an `InitializeAsync` or equivalent, one must be added.
- **`IServiceProvider` retention**: Despite REQ-019 suggesting removal, `IServiceProvider` is still needed for popups requiring pre-push ViewModel configuration (callbacks, initialization). The refactoring should minimize but not eliminate `IServiceProvider` usage.

### Execution Order

> Steps that can run in parallel are marked with ⟳. Steps that must be sequential are numbered.

1. [Git Flow] Create branch `feature/uxd-popups-migration`
2. [om-mobile-core] Add NuGet package, configure `MauiProgram.cs` with `UseUXDiversPopups()` and `AddTransientPopup<>` registrations; add 2 new methods to `IAppPopupService`; refactor `MauiPopupService` completely; update `ChatViewModel.OpenProjectSwitcherAsync` and `ProjectsViewModel.ShowAddProjectAsync`
3. ⟳ [om-mobile-ui] Convert 6 popup XAML + code-behind from `ContentPage` to `PopupPage`; update `App.xaml` with UXDivers resources; remove popup routes from `AppShell.xaml.cs` (can start once om-mobile-core defines the new close pattern)
4. [om-tester] Write unit tests for new `IAppPopupService` methods on ViewModels
5. [om-reviewer] Full review against spec
6. [Fix loop if needed] Address Critical and Major findings
7. [Git Flow] Finish branch and merge

### Definition of Done

- [ ] All `[REQ-001]` through `[REQ-020]` requirements implemented
- [ ] All `[AC-001]` through `[AC-010]` acceptance criteria satisfied
- [ ] Unit tests written for ViewModel changes (ChatViewModel, ProjectsViewModel)
- [ ] `om-reviewer` verdict: Approved or Approved with remarks
- [ ] Git Flow branch finished and deleted
- [ ] Spec moved to `specs/done/` with Completed status
