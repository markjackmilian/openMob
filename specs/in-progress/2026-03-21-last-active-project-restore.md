# Last Active Project Restore on Startup

## Metadata
| Field   | Value                        |
|---------|------------------------------|
| Date    | 2026-03-21                   |
| Status  | In Progress                  |
| Version | 1.0                          |

---

## Executive Summary

Attualmente, all'avvio dell'app, viene sempre selezionato un progetto di default indipendentemente da quale progetto l'utente stesse usando in precedenza. Questa spec introduce la persistenza dell'ultimo progetto attivo in SQLite: al successivo avvio, l'app ripristina automaticamente il progetto che l'utente stava usando, migliorando la continuità d'uso. Se il progetto salvato non è più disponibile, l'app ricade sul primo progetto disponibile.

---

## Scope

### In Scope
- Nuovo servizio `IAppStateService` con metodi per leggere e scrivere l'ID dell'ultimo progetto attivo
- Persistenza dell'ID in SQLite tramite una nuova entità/tabella `AppState` (o equivalente)
- EF Core migration per la nuova struttura dati
- `SplashViewModel` legge l'ultimo progetto attivo al startup e lo usa per determinare il progetto da impostare come attivo
- Aggiornamento dell'ID persistito ogni volta che il progetto attivo cambia nell'app (es. dal drawer o project switcher)
- Fallback: se l'ID salvato non corrisponde a nessun progetto esistente → primo progetto disponibile
- Fallback: se non esiste alcun progetto → comportamento attuale invariato (onboarding o schermata di default)

### Out of Scope
- Ripristino dell'ultima sessione attiva (solo il progetto viene ripristinato)
- Modifiche alla `ProjectSwitcherPage`, al flusso di onboarding o alla `ServerManagementPage`
- Sincronizzazione dello stato tra dispositivi
- Qualsiasi forma di persistenza remota o cloud

---

## Functional Requirements

> Requirements are numbered for traceability.

1. **[REQ-001]** Deve esistere un meccanismo di persistenza globale in SQLite per memorizzare l'ID dell'ultimo progetto attivo. La struttura dati deve supportare una singola riga di stato globale dell'app (chiave/valore o entità dedicata `AppState`).

2. **[REQ-002]** Deve essere definita l'interfaccia `IAppStateService` in `openMob.Core` con almeno i seguenti metodi:
   - `Task<string?> GetLastActiveProjectIdAsync(CancellationToken ct = default)` — restituisce l'ID dell'ultimo progetto attivo, o `null` se non è mai stato impostato.
   - `Task SetLastActiveProjectIdAsync(string projectId, CancellationToken ct = default)` — persiste l'ID del progetto attivo corrente.

3. **[REQ-003]** `IAppStateService` deve essere registrato nel DI container tramite `AddOpenMobCore()` con lifetime Singleton.

4. **[REQ-004]** Ogni volta che il progetto attivo cambia nell'app, `IAppStateService.SetLastActiveProjectIdAsync` deve essere invocato per aggiornare il valore persistito in SQLite.

5. **[REQ-005]** Al startup, `SplashViewModel` (dopo il controllo di raggiungibilità del server già esistente) deve:
   1. Leggere l'ultimo progetto attivo tramite `IAppStateService.GetLastActiveProjectIdAsync`.
   2. Verificare che il progetto esista ancora tra i progetti disponibili tramite `IProjectService`.
   3. Se esiste: impostarlo come progetto attivo prima di navigare a `ChatPage`.
   4. Se non esiste o è `null`: selezionare il primo progetto disponibile (fallback).
   5. Se non esistono progetti: mantenere il comportamento attuale invariato.

6. **[REQ-006]** Il fallback "primo progetto disponibile" utilizza l'ordinamento già applicato da `IProjectService.GetProjectsAsync` (l'ordine è delegato all'implementazione esistente del servizio).

7. **[REQ-007]** Deve essere creata una EF Core migration per la nuova struttura dati SQLite.

---

## Functional Impacts

### Affected Components / Systems
| Component | Impact | Notes |
|-----------|--------|-------|
| `SplashViewModel` | Modificato | Aggiunta lettura ultimo progetto attivo e logica di selezione |
| `FlyoutViewModel` (o ViewModel che gestisce il cambio progetto) | Modificato | Aggiunta chiamata a `SetLastActiveProjectIdAsync` al cambio progetto |
| `IAppStateService` | Nuovo | Interfaccia e implementazione SQLite in `openMob.Core` |
| `AppDbContext` | Modificato | Nuova entità/tabella `AppState` |
| EF Core Migrations | Nuovo | Migration per la tabella `AppState` |
| `AddOpenMobCore()` | Modificato | Registrazione di `IAppStateService` |

### Dependencies
- `IProjectService.GetProjectsAsync` — già esistente; usato per verificare l'esistenza del progetto salvato e per il fallback
- Flusso di startup `SplashViewModel` — già modificato dalla spec `server-offline-startup-navigation` (2026-03-21); questa spec si innesta su quel flusso
- `FlyoutViewModel` — già gestisce il concetto di progetto attivo (spec `drawer-sessions-delete-refactor`, 2026-03-21)

---

## Open Questions & Clarifications

| # | Question | Status | Answer / Decision |
|---|----------|--------|-------------------|
| 1 | Esiste già una tabella di configurazione globale in SQLite dove aggiungere una colonna, o va creata una nuova tabella `AppState`? | Resolved | No, non esiste una tabella di configurazione globale. Le uniche tabelle sono `ServerConnections` e `ProjectPreferences`. Si crea una nuova tabella `AppState` con schema key-value: `Key TEXT PRIMARY KEY, Value TEXT NULL`. |
| 2 | Il cambio progetto avviene solo dal drawer (`FlyoutViewModel`), o ci sono altri punti nell'app (es. project switcher, onboarding)? | Resolved | Il cambio progetto avviene in 3 punti: (1) `FlyoutViewModel.NewSessionAsync` (auto-select first project), (2) `ProjectSwitcherViewModel.SelectProjectAsync`, (3) `ProjectDetailViewModel`. Tutti usano `IActiveProjectService.SetActiveProjectAsync`. La soluzione migliore è integrare la persistenza direttamente in `ActiveProjectService.SetActiveProjectAsync` anziché in ogni singolo ViewModel. |
| 3 | Qual è l'ordinamento esatto restituito da `IProjectService.GetProjectsAsync`? (per nome, per data di creazione, ecc.) | Resolved | `GetAllProjectsAsync` chiama `GET /project` sul server opencode. L'ordinamento è quello restituito dal server (non specificato, probabilmente per worktree path). Il fallback usa `projects.FirstOrDefault()`. |

---

## Acceptance Criteria

> Each criterion maps to one or more functional requirements.

- [ ] **[AC-001]** Dato che l'utente ha usato il progetto X come ultimo progetto attivo, quando riapre l'app, allora il progetto attivo all'avvio è X. *(REQ-001, REQ-002, REQ-005)*
- [ ] **[AC-002]** Dato che il progetto X salvato non esiste più tra i progetti disponibili, quando l'app si avvia, allora viene selezionato il primo progetto disponibile. *(REQ-005, REQ-006)*
- [ ] **[AC-003]** Dato che non è mai stato impostato un ultimo progetto attivo (`null`), quando l'app si avvia, allora viene selezionato il primo progetto disponibile. *(REQ-005, REQ-006)*
- [ ] **[AC-004]** Dato che non esistono progetti, quando l'app si avvia, allora il comportamento è invariato rispetto allo stato attuale. *(REQ-005)*
- [ ] **[AC-005]** Dato che l'utente cambia progetto attivo, quando il cambio avviene, allora il nuovo ID viene immediatamente persistito in SQLite. *(REQ-004)*
- [ ] **[AC-006]** `IAppStateService` è registrato come Singleton nel DI container. *(REQ-003)*

---

## Notes for Technical Analysis

> This section is addressed to the agent that will perform the technical implementation analysis.

- **Flusso di startup esistente**: `SplashViewModel` è già stato modificato dalla spec `server-offline-startup-navigation` (2026-03-21). La logica di selezione del progetto attivo deve inserirsi **dopo** il controllo di raggiungibilità del server e **prima** della navigazione a `ChatPage`. Verificare l'esatto punto di innesto nel metodo `InitializeAsync` (o equivalente) di `SplashViewModel`.

- **Progetto attivo — meccanismo attuale**: dalla spec `drawer-sessions-delete-refactor` (2026-03-21), `FlyoutViewModel` gestisce il concetto di progetto attivo. Verificare come viene attualmente impostato il progetto attivo (quale proprietà/servizio lo detiene) per capire dove agganciare la chiamata a `SetLastActiveProjectIdAsync`.

- **Struttura dati SQLite**: valutare se aggiungere una colonna `LastActiveProjectId` a una tabella di configurazione globale già esistente (es. `ServerConfig` o simile) oppure creare una nuova tabella `AppState` con schema `(Key TEXT PRIMARY KEY, Value TEXT)` o entità dedicata. Preferire la soluzione meno invasiva.

- **Pattern di persistenza**: come stabilito nelle spec precedenti (`session-context-sheet-1of3-core`), tutte le preferenze sono persistite in SQLite tramite EF Core. `IAppStateService` deve seguire lo stesso pattern (repository/service con `AppDbContext`).

- **Nessun `ConfigureAwait(false)` nei ViewModel**: come stabilito dalla spec `drawer-sessions-delete-refactor`, i ViewModel non devono usare `ConfigureAwait(false)`. I servizi/repository possono usarlo.

- **File probabilmente coinvolti**:
  - `src/openMob.Core/Data/Entities/AppState.cs` (nuovo) o modifica a entità esistente
  - `src/openMob.Core/Data/AppDbContext.cs`
  - `src/openMob.Core/Data/Migrations/` (nuova migration)
  - `src/openMob.Core/Services/IAppStateService.cs` (nuovo)
  - `src/openMob.Core/Services/AppStateService.cs` (nuovo)
  - `src/openMob.Core/ViewModels/SplashViewModel.cs`
  - `src/openMob.Core/ViewModels/FlyoutViewModel.cs` (o ViewModel che gestisce il cambio progetto)
  - `src/openMob/MauiProgram.cs` o `CoreServiceExtensions.cs`
  - `tests/openMob.Tests/ViewModels/SplashViewModelTests.cs`
  - `tests/openMob.Tests/Services/AppStateServiceTests.cs` (nuovo)

---

## Technical Analysis

> Added by: om-orchestrator | Date: 2026-03-21

### Change Classification

| Field | Value |
|-------|-------|
| Change type | Feature |
| Git Flow branch | feature/last-active-project-restore |
| Branches from | develop |
| Estimated complexity | Medium |
| Estimated agents involved | om-mobile-core, om-tester, om-reviewer |

### Layers Involved

| Layer | Agent | Scope |
|-------|-------|-------|
| Data / EF Core | om-mobile-core | src/openMob.Core/Data/Entities/, src/openMob.Core/Data/AppDbContext.cs, src/openMob.Core/Data/Migrations/ |
| Business logic / Services | om-mobile-core | src/openMob.Core/Services/ |
| ViewModels | om-mobile-core | src/openMob.Core/ViewModels/SplashViewModel.cs, src/openMob.Core/ViewModels/ActiveProjectService.cs |
| DI Registration | om-mobile-core | src/openMob.Core/Infrastructure/DI/CoreServiceExtensions.cs |
| Unit Tests | om-tester | tests/openMob.Tests/ |
| Code Review | om-reviewer | all of the above |

### Files to Create

- `src/openMob.Core/Data/Entities/AppState.cs` — EF Core entity with `Key` (string PK) and `Value` (string? nullable) for key-value global app state
- `src/openMob.Core/Services/IAppStateService.cs` — interface with `GetLastActiveProjectIdAsync` and `SetLastActiveProjectIdAsync`
- `src/openMob.Core/Services/AppStateService.cs` — implementation using `AppDbContext` with `IServiceScopeFactory` (since service is Singleton but DbContext is Scoped)
- `src/openMob.Core/Data/Migrations/20260321000000_AddAppStateTable.cs` — EF Core migration for the new `AppState` table
- `tests/openMob.Tests/Services/AppStateServiceTests.cs` — unit tests for AppStateService

### Files to Modify

- `src/openMob.Core/Data/AppDbContext.cs` — add `DbSet<AppState> AppStates` and configure entity in `OnModelCreating`
- `src/openMob.Core/Services/ActiveProjectService.cs` — inject `IAppStateService`, call `SetLastActiveProjectIdAsync` inside `SetActiveProjectAsync` after successful project activation
- `src/openMob.Core/ViewModels/SplashViewModel.cs` — inject `IActiveProjectService` and `IAppStateService`, add project restore logic after server reachability check and before navigation to ChatPage
- `src/openMob.Core/Infrastructure/DI/CoreServiceExtensions.cs` — register `IAppStateService` as Singleton

### Technical Dependencies

- `IProjectService.GetAllProjectsAsync` — already exists, used for fallback project selection
- `IActiveProjectService.SetActiveProjectAsync` — already exists, used to set the restored project as active
- `AppDbContext` is registered as Scoped; `IAppStateService` is Singleton → must use `IServiceScopeFactory` to create scoped DbContext instances inside the Singleton service
- No new NuGet packages required
- No API endpoints involved (all local SQLite persistence)

### Technical Risks

- **Singleton + Scoped DbContext**: `IAppStateService` is Singleton but `AppDbContext` is Scoped. Must use `IServiceScopeFactory` to avoid captive dependency. This is the same pattern used by other Singleton services in the codebase.
- **Race condition on startup**: `SplashViewModel.InitializeAsync` runs once. The project restore logic must complete before navigation. No race condition expected since it's sequential.
- **Migration on existing databases**: The new `AppState` table is additive — no breaking changes to existing data. `AppState` starts empty; first `SetLastActiveProjectIdAsync` call creates the row.

### Execution Order

> Steps that can run in parallel are marked with ⟳. Steps that must be sequential are numbered.

1. [Git Flow] Create branch `feature/last-active-project-restore`
2. [om-mobile-core] Create `AppState` entity, migration, `IAppStateService`/`AppStateService`, modify `ActiveProjectService` and `SplashViewModel`, register in DI
3. [om-tester] Write unit tests for `AppStateService`, updated `SplashViewModel`, updated `ActiveProjectService`
4. [om-reviewer] Full review against spec
5. [Fix loop if needed] Address Critical and Major findings
6. [Git Flow] Finish branch and merge

### Definition of Done

- [ ] All `[REQ-001]` through `[REQ-007]` requirements implemented
- [ ] All `[AC-001]` through `[AC-006]` acceptance criteria satisfied
- [ ] Unit tests written for `AppStateService`, updated `SplashViewModel`, updated `ActiveProjectService`
- [ ] `om-reviewer` verdict: ✅ Approved or ⚠️ Approved with remarks
- [ ] Git Flow branch finished and deleted
- [ ] Spec moved to `specs/done/` with Completed status
