# UXDivers Popups Migration

## Metadata
| Field   | Value                        |
|---------|------------------------------|
| Date    | 2026-03-22                   |
| Status  | Draft                        |
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
| `src/openMob.Core/ViewModels/ChatViewModel.cs` | `OpenProjectSwitcherAsync` delega a `IAppPopupService` | Aggiungere `IAppPopupService` come dipendenza se assente |
| `src/openMob.Core/ViewModels/ProjectsViewModel.cs` | `ShowAddProjectAsync` delega a `IAppPopupService` | Rimuovere toast placeholder |

### Dependencies
- `UXDivers.Popups.Maui` v0.9.4 — Apache License 2.0, disponibile su NuGet.org
- `CommunityToolkit.Maui` già presente — nessun conflitto noto con UXDivers Popups
- `IAppPopupService` (già in `openMob.Core`) — interfaccia estesa, non sostituita
- Pattern "initialize before push" già stabilito in `session-context-sheet-1of3-core` per `ShowContextSheetAsync` — da applicare uniformemente a `ShowProjectSwitcherAsync`

---

## Open Questions & Clarifications

| # | Question | Status | Answer / Decision |
|---|----------|--------|-------------------|
| 1 | `SimpleActionPopup` e `FormPopup` di UXDivers implementano `PopupResultPage<T>` per restituire un risultato tipizzato? Se sì, `ShowConfirmDeleteAsync` e `ShowRenameAsync` possono usare `await IPopupService.Current.PushAsync<SimpleActionPopup, bool>()`. Se no, serve un custom popup o un pattern con `TaskCompletionSource`. | Open | Da verificare nella doc UXDivers o nel codice sorgente della libreria prima dell'implementazione. |
| 2 | `ProjectSwitcherSheet.OnAppearing` chiama `LoadProjectsCommand.ExecuteAsync(null)`. Con `PopupPage`, il lifecycle `OnAppearing` è ancora garantito? Oppure il caricamento deve essere spostato nel metodo `ShowProjectSwitcherAsync` di `MauiPopupService` (pattern "initialize before push")? | Open | Raccomandato: spostare il caricamento in `MauiPopupService.ShowProjectSwitcherAsync` per coerenza con il pattern già stabilito. Da confermare in implementazione. |
| 3 | `MauiPopupService` è registrato come `Singleton` in `MauiProgram.cs`. Con UXDivers, `IPopupService.Current` è un singleton statico — nessun conflitto atteso, ma da verificare che non ci siano problemi di thread safety con l'accesso statico da un singleton. | Open | Probabilmente non critico; da verificare in fase di implementazione. |

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

1. **`SimpleActionPopup` e `FormPopup` come result popup** — Verificare se questi built-in di UXDivers estendono `PopupResultPage<T>`. Se sì, `ShowConfirmDeleteAsync` usa `PushAsync<SimpleActionPopup, bool>()` e `ShowRenameAsync` usa `PushAsync<FormPopup, List<string?>>()`. Se no, valutare: (a) custom `PopupResultPage<bool>` per la conferma, (b) `TaskCompletionSource<T>` con callback, oppure (c) mantenere `DisplayAlert`/`DisplayPrompt` nativi solo per questi due casi (scelta pragmatica accettabile se i built-in non supportano result).

2. **`ProjectSwitcherSheet.OnAppearing` lifecycle** — Con `PopupPage`, `OnAppearing` è chiamato dopo l'animazione di apertura. Il caricamento dei progetti in `OnAppearing` può causare un flash di lista vuota. Raccomandato: spostare `LoadProjectsCommand.ExecuteAsync` in `MauiPopupService.ShowProjectSwitcherAsync` prima del push (pattern "initialize before push" già stabilito in `session-context-sheet-1of3-core`). Questo richiede di rimuovere l'`OnAppearing` override da `ProjectSwitcherSheet`.

3. **`IServiceProvider` in `MauiPopupService`** — Con `AddTransientPopup<TPopup, TViewModel>()`, UXDivers gestisce la risoluzione DI internamente quando si usa `PushAsync<TPopup>()`. Tuttavia, per i popup che richiedono inizializzazione pre-push (ContextSheet, ProjectSwitcherSheet), è ancora necessario risolvere l'istanza manualmente per accedere al ViewModel prima del push. Opzioni: (a) mantenere `IServiceProvider` solo per questi casi, (b) usare `PushAsync<TPopup>(parameters)` con `OnNavigatedTo`/`IPopupViewModel.OnPopupNavigatedAsync` per passare i dati. Valutare quale approccio è più pulito rispetto al pattern esistente.

4. **Rimozione route Shell** — Le route `project-switcher`, `agent-picker`, `model-picker`, `add-project` in `AppShell.xaml.cs` devono essere rimosse. Verificare che nessun ViewModel o View le usi ancora tramite `INavigationService.GoToAsync(...)` prima della rimozione.

5. **`ChatViewModel` e `IAppPopupService`** — Verificare se `ChatViewModel` ha già `IAppPopupService` come dipendenza (probabile, dato che usa `ShowContextSheetAsync`). Se sì, `OpenProjectSwitcherAsync` può delegare direttamente senza modifiche al costruttore.

6. **`MauiPopupService` come Singleton con `IPopupService.Current` statico** — `IPopupService.Current` è un singleton statico gestito da UXDivers. Non ci sono problemi di lifetime, ma verificare che l'accesso statico sia thread-safe (la doc UXDivers non menziona restrizioni di thread).

### Suggested implementation approach

1. Aggiungere il NuGet e configurare `MauiProgram.cs` + `App.xaml` (REQ-001/002/003).
2. Convertire i 6 XAML + code-behind da `ContentPage` a `PopupPage` (REQ-004).
3. Aggiornare le registrazioni DI con `AddTransientPopup<>` (REQ-005).
4. Aggiornare `IAppPopupService` con i 2 nuovi metodi (REQ-012/013).
5. Refactoring `MauiPopupService` metodo per metodo (REQ-006/007/008/009/010/011/014/015).
6. Aggiornare `ChatViewModel` e `ProjectsViewModel` (REQ-016/017).
7. Rimuovere le route Shell (REQ-018).
8. Build + smoke test.

### Constraints to respect

- `openMob.Core` ha **zero dipendenze MAUI** — `IPopupService` di UXDivers non deve mai essere importato in `openMob.Core`. L'interfaccia `IAppPopupService` rimane l'unica astrazione popup nel Core.
- Il nome `IAppPopupService` è stato scelto esplicitamente per evitare collisione con `IPopupService` di UXDivers (documentato nel file sorgente). Questa convenzione deve essere mantenuta.
- Tutti i popup rimangono `Transient` — nessuno deve diventare `Singleton` senza motivazione esplicita.
- Il pattern "initialize before push" (inizializzare il ViewModel prima di `PushAsync`) deve essere applicato uniformemente a tutti i popup che richiedono dati pre-caricati.
- `async void` è vietato eccetto negli handler MAUI lifecycle (`OnAppearing`, ecc.).

### Related files or modules

- `src/openMob/Services/MauiPopupService.cs` — file principale da refactoring
- `src/openMob.Core/Services/IAppPopupService.cs` — interfaccia da estendere
- `src/openMob/MauiProgram.cs` — DI composition root
- `src/openMob/App.xaml` — resource dictionaries
- `src/openMob/AppShell.xaml.cs` — route cleanup
- `src/openMob/Views/Popups/` — tutti e 6 i popup
- `src/openMob.Core/ViewModels/ChatViewModel.cs` — `OpenProjectSwitcherAsync`
- `src/openMob.Core/ViewModels/ProjectsViewModel.cs` — `ShowAddProjectAsync`
- Past spec: `session-context-sheet-1of3-core` — pattern "initialize before push" per `ShowContextSheetAsync`
- Past spec: `drawer-sessions-delete-refactor` — `ContextSheetViewModel.DeleteSessionCommand` e `PopPopupAsync` (origine del crash)
