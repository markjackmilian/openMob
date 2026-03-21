# Onboarding Wizard — Replace Provider Step with Default Model Selection

## Metadata
| Field   | Value                        |
|---------|------------------------------|
| Date    | 2026-03-21                   |
| Status  | In Progress                  |
| Version | 1.0                          |

---

## Executive Summary

Il wizard di onboarding (primo avvio, nessun server configurato) presenta attualmente uno step di scelta provider che deve essere rimosso. Al suo posto viene introdotto uno step di selezione del modello di default, che appare immediatamente dopo che la connessione al server opencode è andata a buon fine. La scelta del modello è obbligatoria per completare il wizard. Il modello selezionato viene persistito come `DefaultModelId` sull'entità server nel database SQLite locale. La configurazione dei provider è responsabilità del server opencode e non dell'app mobile.

---

## Scope

### In Scope
- Rimozione dello step di scelta provider dal wizard di onboarding
- Aggiunta di un nuovo step "Scelta modello di default" posizionato dopo lo step di connessione server (esito positivo)
- Il nuovo step carica la lista dei modelli disponibili tramite API dal server appena connesso
- Il modello selezionato viene salvato come `DefaultModelId` (`string?`) sull'entità server nel DB SQLite locale
- EF Core migration per aggiungere la colonna `DefaultModelId` alla tabella server
- Estensione del repository server con metodi per leggere e scrivere `DefaultModelId`
- La scelta del modello è obbligatoria: il wizard non può essere completato senza una selezione
- Se il fetch dei modelli fallisce, lo step mostra un errore e blocca l'avanzamento (no skip)
- Aggiunta nella `ServerDetailPage` di una voce per visualizzare e modificare il modello di default del server

### Out of Scope
- Modifiche al flusso di startup/splash (già gestito dalla spec `server-offline-startup-navigation`)
- Selezione del modello a livello di progetto (già gestita da `ProjectPreference` tramite `ContextSheet`)
- Retry automatico del fetch modelli in caso di errore
- Qualsiasi modifica alla `ServerManagementPage`
- Sincronizzazione o scrittura del modello di default sul server opencode (tutto locale)
- Relazione tra `Server.DefaultModelId` e `ProjectPreference.DefaultModelId` (futura spec)

---

## Functional Requirements

> Requirements are numbered for traceability.

1. **[REQ-001]** Lo step di scelta provider viene rimosso dal wizard di onboarding. La sequenza degli step risultante non deve contenere alcun riferimento a provider AI.

2. **[REQ-002]** Dopo lo step di connessione server, se la connessione è andata a buon fine, il wizard avanza automaticamente a un nuovo step: "Scelta modello di default".

3. **[REQ-003]** All'ingresso nel nuovo step, il `OnboardingViewModel` (o il ViewModel dedicato allo step) chiama l'API del server opencode per recuperare la lista dei modelli disponibili. Durante il caricamento viene mostrato un indicatore di attività (`IsBusy = true`).

4. **[REQ-004]** Se il fetch dei modelli ha esito negativo (qualsiasi errore di rete o HTTP), viene mostrato un messaggio di errore nello step. Il pulsante "Avanti" / "Completa" rimane disabilitato. Non è previsto avanzamento automatico né skip dello step.

5. **[REQ-005]** La lista dei modelli viene presentata all'utente come lista selezionabile. L'utente deve selezionare esattamente un modello.

6. **[REQ-006]** Il pulsante di completamento del wizard è abilitato solo quando un modello è selezionato (`SelectedModelId != null`).

7. **[REQ-007]** Al completamento del wizard, il `DefaultModelId` selezionato viene salvato sull'entità server nel DB SQLite tramite il repository server.

8. **[REQ-008]** L'entità server viene estesa con il campo `DefaultModelId` (`string?`, nullable). Il valore `null` indica che nessun modello di default è stato impostato.

9. **[REQ-009]** Viene creata una EF Core migration per aggiungere la colonna `DefaultModelId TEXT NULL` alla tabella server. Le righe esistenti ricevono il valore `NULL`.

10. **[REQ-010]** Il repository server (interfaccia e implementazione) viene esteso con almeno i seguenti metodi:
    - `Task<string?> GetDefaultModelAsync(string serverId, CancellationToken ct)` — legge il `DefaultModelId` del server
    - `Task<bool> SetDefaultModelAsync(string serverId, string modelId, CancellationToken ct)` — scrive il `DefaultModelId`

11. **[REQ-011]** Nella `ServerDetailPage`, viene aggiunta una voce "Modello di default" che mostra il modello attualmente impostato (o "Nessun modello" se `null`). Il tap sulla voce apre un picker/lista modelli (caricati dal server) che permette di modificare la selezione e salvarla.

12. **[REQ-012]** Il fetch dei modelli per lo step del wizard e per il picker nella `ServerDetailPage` utilizza lo stesso servizio/metodo esistente usato dal `ContextSheet` (riuso dell'infrastruttura già presente).

---

## Functional Impacts

### Affected Components / Systems
| Component | Impact | Notes |
|-----------|--------|-------|
| `OnboardingPage` / `OnboardingViewModel` | Modifica — rimozione step provider, aggiunta step modello | Step provider rimosso, nuovo step modello aggiunto dopo connessione server |
| Entità `Server` (o `ServerConnection`) | Estensione — nuovo campo `DefaultModelId string?` | Campo nullable, default NULL |
| EF Core migration | Nuova migration | Aggiunge colonna `DefaultModelId` alla tabella server |
| `IServerRepository` / `ServerRepository` | Estensione — nuovi metodi `GetDefaultModelAsync`, `SetDefaultModelAsync` | Pattern coerente con `IProjectPreferenceService.SetDefaultModelAsync` |
| `ServerDetailPage` / `ServerDetailViewModel` | Estensione — nuova voce "Modello di default" con picker | Carica modelli dal server, salva via repository |
| `IModelService` / `ModelService` (o equivalente) | Riuso — nessuna modifica prevista | Il fetch modelli è già implementato per il `ContextSheet` |

### Dependencies
- Spec completata `session-context-sheet-2of3-agent-model` — definisce il pattern di fetch modelli e il `ModelPickerSheet` già esistente; da riutilizzare ove possibile
- Spec completata `server-offline-startup-navigation` — definisce il flusso di startup; il caso "nessun server configurato → OnboardingPage" rimane invariato
- API opencode `GET /provider` (o endpoint equivalente) — da verificare in codebase (`OpencodeApiClient`)

---

## Open Questions & Clarifications

| # | Question | Status | Answer / Decision |
|---|----------|--------|-------------------|
| 1 | Qual è l'endpoint esatto per recuperare i modelli dal server opencode? (es. `GET /model`, `GET /models`) | Resolved | I modelli vengono estratti da `IProviderService.GetConfiguredProvidersAsync()` che chiama `GET /config/providers`. Ogni `ProviderDto` ha un campo `Models` (JsonElement) da cui si estraggono i modelli. Il pattern è già implementato in `ModelPickerViewModel.ExtractModelsFromProvider`. |
| 2 | Il `Server.DefaultModelId` è pensato come fallback globale quando `ProjectPreference.DefaultModelId` è null, oppure è un concetto indipendente usato solo in fase di configurazione server? | Open | Impatta l'architettura di risoluzione del modello attivo — da decidere in una futura spec dedicata |
| 3 | Il `ModelPickerSheet` già esistente (introdotto da `session-context-sheet-2of3-agent-model`) può essere riutilizzato direttamente nel wizard e nella `ServerDetailPage`, oppure richiede adattamenti? | Resolved | `ModelPickerViewModel` è già parametrizzabile tramite `OnModelSelected` callback e `SelectedModelId`. Può essere riutilizzato nella `ServerDetailPage` tramite `IAppPopupService.ShowModelPicker`. Per il wizard, la logica di model loading viene integrata direttamente in `OnboardingViewModel` (inline step, non popup). |

---

## Acceptance Criteria

> Each criterion maps to one or more functional requirements.

- [ ] **[AC-001]** Dato che nessun server è configurato, quando si avvia l'app per la prima volta, il wizard non mostra alcuno step di scelta provider. *(REQ-001)*

- [ ] **[AC-002]** Dato che la connessione al server è andata a buon fine nello step precedente, quando il wizard avanza, viene mostrato lo step di selezione modello con la lista dei modelli caricata dal server. *(REQ-002, REQ-003)*

- [ ] **[AC-003]** Dato che il fetch dei modelli fallisce (errore di rete o HTTP), quando si è nello step di selezione modello, viene mostrato un messaggio di errore e il pulsante di completamento rimane disabilitato. *(REQ-004)*

- [ ] **[AC-004]** Dato che la lista modelli è caricata correttamente, quando l'utente non ha ancora selezionato un modello, il pulsante di completamento del wizard è disabilitato. *(REQ-005, REQ-006)*

- [ ] **[AC-005]** Dato che l'utente seleziona un modello e completa il wizard, quando si naviga alla schermata principale, il `DefaultModelId` del server è salvato correttamente nel DB SQLite. *(REQ-007)*

- [ ] **[AC-006]** Dato che si ispeziona il DB dopo il completamento del wizard, la colonna `DefaultModelId` della tabella server contiene l'ID del modello selezionato. *(REQ-008, REQ-009)*

- [ ] **[AC-007]** Dato che si apre la `ServerDetailPage` di un server configurato, quando si visualizza la voce "Modello di default", viene mostrato il modello attualmente impostato (o "Nessun modello" se null). *(REQ-011)*

- [ ] **[AC-008]** Dato che si modifica il modello di default dalla `ServerDetailPage`, quando si salva la selezione, il nuovo `DefaultModelId` è persistito nel DB SQLite. *(REQ-011)*

- [ ] **[AC-009]** Il fetch dei modelli nel wizard e nella `ServerDetailPage` utilizza la stessa infrastruttura di servizio già usata dal `ContextSheet`. *(REQ-012)*

---

## Notes for Technical Analysis

> This section is addressed to the agent that will perform the technical implementation analysis.

- **Endpoint modelli**: Verificare in `OpencodeApiClient` quale endpoint viene usato per il fetch dei modelli (probabilmente `GET /model`). Il `ModelService` o equivalente è già implementato per il `ContextSheet` — riutilizzare senza duplicare.

- **Entità server**: Identificare il nome esatto dell'entità e della tabella server nel DB (potrebbe essere `Server`, `ServerConnection`, o simile). Verificare in `AppDbContext` e nelle migration esistenti.

- **Pattern migration**: Come stabilito nelle spec precedenti (es. `session-context-sheet-1of3-core`), le migration sono scritte a mano con timestamp `YYYYMMDDHHMMSS`. La nuova migration aggiunge `DefaultModelId TEXT NULL` con `DEFAULT NULL` per le righe esistenti.

- **Pattern repository**: Il pattern `SetDefaultModelAsync` è già presente in `IProjectPreferenceService`. Applicare lo stesso pattern al repository server per coerenza.

- **Riuso ModelPickerSheet**: Verificare se il `ModelPickerSheet` (introdotto da `session-context-sheet-2of3-agent-model`) è parametrizzabile per essere usato sia nel wizard che nella `ServerDetailPage`. Se non è riutilizzabile direttamente, valutare se estrarre un `ModelPickerViewModel` condiviso.

- **OnboardingViewModel**: Verificare la struttura attuale del wizard (numero di step, navigazione tra step, ViewModel/Page coinvolti) per capire dove inserire il nuovo step e dove rimuovere quello del provider.

- **Constraint**: Tutto il salvataggio è locale (SQLite). Nessuna scrittura verso il server opencode. Il `DefaultModelId` è un campo dell'app, non del server.

- **Relazione `Server.DefaultModelId` vs `ProjectPreference.DefaultModelId`**: La semantica di questa relazione (fallback vs indipendenza) è una open question — non implementare alcuna logica di fallback in questa spec. Limitarsi a salvare e leggere il valore sull'entità server.

---

## Technical Analysis

> Added by: om-orchestrator | Date: 2026-03-21

### Change Classification

| Field | Value |
|-------|-------|
| Change type | Feature |
| Git Flow branch | feature/onboarding-default-model |
| Branches from | develop |
| Estimated complexity | Medium |
| Estimated agents involved | om-mobile-core, om-mobile-ui, om-tester, om-reviewer |

### Layers Involved

| Layer | Agent | Scope |
|-------|-------|-------|
| Data / EF Core | om-mobile-core | src/openMob.Core/Data/Entities/ServerConnection.cs, src/openMob.Core/Data/AppDbContext.cs, src/openMob.Core/Data/Migrations/ |
| Repositories | om-mobile-core | src/openMob.Core/Data/Repositories/IServerConnectionRepository.cs, ServerConnectionRepository.cs |
| ViewModels | om-mobile-core | src/openMob.Core/ViewModels/OnboardingViewModel.cs, src/openMob.Core/ViewModels/ServerDetailViewModel.cs |
| XAML Views | om-mobile-ui | src/openMob/Views/Pages/OnboardingPage.xaml, src/openMob/Views/Controls/OnboardingModelSelectionView.xaml (new), src/openMob/Views/Pages/ServerDetailPage.xaml |
| Unit Tests | om-tester | tests/openMob.Tests/ |
| Code Review | om-reviewer | all of the above |

### Files to Create

- `src/openMob.Core/Data/Migrations/20260321010000_AddDefaultModelIdToServerConnections.cs` — EF Core migration adding `DefaultModelId TEXT NULL` to `ServerConnections` table
- `src/openMob/Views/Controls/OnboardingModelSelectionView.xaml` + `.xaml.cs` — new onboarding step control for model selection (replaces provider setup step)
- `tests/openMob.Tests/ViewModels/OnboardingViewModelDefaultModelTests.cs` — tests for the new model selection step logic

### Files to Modify

- `src/openMob.Core/Data/Entities/ServerConnection.cs` — add `DefaultModelId` property (`string?`, max 500)
- `src/openMob.Core/Data/AppDbContext.cs` — configure `DefaultModelId` column in `OnModelCreating`
- `src/openMob.Core/Data/Repositories/IServerConnectionRepository.cs` — add `GetDefaultModelAsync` and `SetDefaultModelAsync` methods
- `src/openMob.Core/Data/Repositories/ServerConnectionRepository.cs` — implement new methods
- `src/openMob.Core/Infrastructure/Dtos/ServerConnectionDto.cs` — add `DefaultModelId` field to DTO record
- `src/openMob.Core/ViewModels/OnboardingViewModel.cs` — remove provider step (step 3), replace with model selection step, change TotalSteps from 5 to 4, add model loading/selection logic, save DefaultModelId on completion
- `src/openMob.Core/ViewModels/ServerDetailViewModel.cs` — add `DefaultModelId` display property, add `ChangeDefaultModelCommand` that opens ModelPickerSheet
- `src/openMob/Views/Pages/OnboardingPage.xaml` — remove `OnboardingProviderSetupView`, add `OnboardingModelSelectionView` at step 3
- `src/openMob/Views/Pages/ServerDetailPage.xaml` — add "Default Model" row with current model display and tap-to-change
- `src/openMob/Views/Controls/OnboardingProviderSetupView.xaml` — DELETE (no longer needed)
- `src/openMob/Views/Controls/OnboardingProviderSetupView.xaml.cs` — DELETE

### Technical Dependencies

- `IProviderService.GetConfiguredProvidersAsync()` — already exists, returns providers with `Models` JsonElement
- `ModelPickerViewModel.ExtractModelsFromProvider` — existing pattern for parsing models from provider JSON; reuse this logic in `OnboardingViewModel`
- `IAppPopupService.ShowModelPicker` — for `ServerDetailPage` model picker (if this method exists; otherwise use `PushPopup` with `ModelPickerSheet`)
- `IServerConnectionRepository` — existing repository, needs extension with 2 new methods
- No new NuGet packages required

### Technical Risks

- **Wizard step renumbering**: Changing from 5 steps to 4 steps affects `CurrentStep` logic, `Progress` calculation, `CanGoNext`, `IsStepOptional`, and all step-dependent XAML visibility converters. Must be carefully tested.
- **Model loading failure blocks wizard**: If the server is reachable but returns no configured providers (or models), the user is stuck. The spec explicitly says no skip — this is by design but could be a UX concern.
- **ServerConnectionDto is a record**: Adding `DefaultModelId` to the DTO record requires updating all construction sites (OnboardingViewModel.TestConnectionAsync, ServerDetailViewModel.SaveAsync, TestDataBuilder).

### Execution Order

> Steps that can run in parallel are marked with ⟳. Steps that must be sequential are numbered.

1. [Git Flow] Create branch `feature/onboarding-default-model`
2. [om-mobile-core] Add `DefaultModelId` to entity, migration, repository extension, DTO update, OnboardingViewModel refactor, ServerDetailViewModel extension
3. ⟳ [om-mobile-ui] Create `OnboardingModelSelectionView`, update `OnboardingPage.xaml` (remove provider step, add model step), update `ServerDetailPage.xaml` (can start once ViewModel binding surface is defined)
4. [om-tester] Write unit tests for OnboardingViewModel model step, ServerDetailViewModel default model, repository extension
5. [om-reviewer] Full review against spec
6. [Fix loop if needed] Address Critical and Major findings
7. [Git Flow] Finish branch and merge

### Definition of Done

- [ ] All `[REQ-001]` through `[REQ-012]` requirements implemented
- [ ] All `[AC-001]` through `[AC-009]` acceptance criteria satisfied
- [ ] Unit tests written for all new and modified ViewModels and repository methods
- [ ] `om-reviewer` verdict: ✅ Approved or ⚠️ Approved with remarks
- [ ] Git Flow branch finished and deleted
- [ ] Spec moved to `specs/done/` with Completed status
