# Dynamic Provider & Model Selection

## Metadata
| Field   | Value                        |
|---------|------------------------------|
| Date    | 2026-03-16                   |
| Status  | Draft                        |
| Version | 1.0                          |

---

## Executive Summary

L'app recupera già la lista di provider e modelli disponibili dal server opencode tramite `GET /provider`, e `ModelPickerViewModel` è già in grado di parsificare e strutturare questi dati. Tuttavia il `ModelPickerSheet.xaml` mostra ancora una lista hardcodata di tre modelli Claude e non esiste alcun meccanismo di comunicazione tra il picker e i ViewModel chiamanti. Questa feature completa il collegamento: rende il picker dinamico, introduce la persistenza SQLite del modello di default a livello progetto, e propaga il modello selezionato alla `ChatPage` sia al caricamento che in-memory durante la sessione.

---

## Scope

### In Scope
- Refactor di `ModelPickerSheet.xaml` per usare i binding dinamici di `ModelPickerViewModel` (rimozione lista hardcodata)
- Stato vuoto nel picker: messaggio "Nessun modello disponibile" + pulsante verso le impostazioni provider
- Implementazione del meccanismo di callback picker → ViewModel chiamante per comunicare il `SelectedModelId`
- Implementazione di `ProjectDetailViewModel.ChangeModelAsync`: apertura picker, ricezione risultato, aggiornamento display e persistenza in SQLite
- Nuova entità EF Core `ProjectPreference` con migrazione SQLite
- Nuovo servizio `IProjectPreferenceService` per CRUD su `ProjectPreference`
- `ChatViewModel`: nuove proprietà `SelectedModelId` e `SelectedModelName`, inizializzate dal `DefaultModelId` del progetto al caricamento
- `ChatViewModel.ShowMoreMenuAsync` — caso "Change model": apertura picker e aggiornamento in-memory di `SelectedModelId` / `SelectedModelName`
- Binding della topbar di `ChatPage` a `SelectedModelName` (rimozione testo hardcodato "Sonnet")
- `SelectedModelId` su `ChatViewModel` esposto nel formato `"providerId/modelId"` pronto per `SendPromptRequest`

### Out of Scope
- Configurazione provider (inserimento API key, flusso OAuth)
- Persistenza del modello selezionato a livello di singola sessione (in-memory è sufficiente)
- Implementazione della send command in `ChatViewModel` (Spec 04)
- Selezione agent (pattern identico ma feature separata)
- Modifica del comportamento di `AgentPickerSheet` / `AgentPickerViewModel`

---

## Functional Requirements

> Requirements are numbered for traceability.

1. **[REQ-001]** `ModelPickerSheet.xaml` deve rimuovere la lista hardcodata e bindare le seguenti proprietà di `ModelPickerViewModel`: `ProviderGroups` (CollectionView raggruppata per provider), `IsLoading` (indicatore di caricamento), `IsEmpty` (stato vuoto), `HasProviders`, e `SelectModelCommand`.

2. **[REQ-002]** Quando `IsLoading` è `true`, il picker mostra un indicatore di attività (spinner) al posto della lista.

3. **[REQ-003]** Quando `IsEmpty` è `true` (nessun provider restituisce modelli), il picker mostra un messaggio "Nessun modello disponibile" e un pulsante che esegue `ConfigureProvidersCommand` (naviga alle impostazioni provider).

4. **[REQ-004]** `ModelPickerViewModel` deve esporre un meccanismo di callback per comunicare il `SelectedModelId` al ViewModel chiamante al momento della selezione. Il meccanismo scelto deve essere compatibile con la testabilità (interfaccia o `Action<string>` iniettata alla costruzione).

5. **[REQ-005]** Creare la nuova entità EF Core `ProjectPreference` con i campi: `ProjectId` (string, PK), `DefaultModelId` (string?, nullable). Creare la relativa migrazione EF Core.

6. **[REQ-006]** Creare `IProjectPreferenceService` e la sua implementazione `ProjectPreferenceService` con i metodi: `GetAsync(projectId, ct) → Task<ProjectPreference?>` e `SetDefaultModelAsync(projectId, modelId, ct) → Task`.

7. **[REQ-007]** `ProjectDetailViewModel.ChangeModelAsync` deve: aprire il `ModelPickerSheet` passando il callback, ricevere il `SelectedModelId` risultante, aggiornare la proprietà osservabile `DefaultModelName` con il nome display del modello (parte dopo `/` del formato `"providerId/modelId"`), e chiamare `IProjectPreferenceService.SetDefaultModelAsync`.

8. **[REQ-008]** `ChatViewModel` deve esporre le proprietà osservabili `SelectedModelId: string?` e `SelectedModelName: string?`. Al caricamento del contesto (`LoadContextAsync`), deve leggere `IProjectPreferenceService.GetAsync(projectId)` e inizializzare entrambe le proprietà se un `DefaultModelId` è presente.

9. **[REQ-009]** `ChatViewModel.ShowMoreMenuAsync` — caso "Change model" — deve aprire il `ModelPickerSheet` tramite `IAppPopupService`, ricevere il modello selezionato via callback, e aggiornare `SelectedModelId` e `SelectedModelName` in-memory senza persistenza.

10. **[REQ-010]** La topbar di `ChatPage.xaml` deve bindare il testo centrale a `SelectedModelName`. Se `SelectedModelName` è null o vuoto, mostrare il testo placeholder "Seleziona modello".

11. **[REQ-011]** `SelectedModelId` su `ChatViewModel` deve essere nel formato `"providerId/modelId"`. Deve essere splittabile con `Split('/', 2)` per ottenere `ProviderId` e `ModelId` da passare a `SendPromptRequest` nella futura send command.

---

## Functional Impacts

### Affected Components / Systems
| Component | Impact | Notes |
|-----------|--------|-------|
| `ModelPickerSheet.xaml` | Refactor XAML — rimozione lista statica, aggiunta binding dinamici e stato vuoto | File: `src/openMob/Views/Popups/ModelPickerSheet.xaml` |
| `ModelPickerViewModel` | Aggiunta meccanismo callback per comunicare `SelectedModelId` al chiamante | File: `src/openMob.Core/ViewModels/ModelPickerViewModel.cs` |
| `ProjectDetailViewModel` | Implementazione `ChangeModelAsync`, dipendenza su `IProjectPreferenceService` | File: `src/openMob.Core/ViewModels/ProjectDetailViewModel.cs` |
| `ChatViewModel` | Aggiunta `SelectedModelId`, `SelectedModelName`, caricamento da DB, gestione "Change model" | File: `src/openMob.Core/ViewModels/ChatViewModel.cs` |
| `ChatPage.xaml` | Binding topbar a `SelectedModelName`, rimozione testo hardcodato | File: `src/openMob/Views/Pages/ChatPage.xaml` |
| `ProjectPreference` (nuova entità) | Nuova tabella SQLite con migrazione EF Core | File: `src/openMob.Core/Data/Entities/ProjectPreference.cs` |
| `AppDbContext` | Aggiunta `DbSet<ProjectPreference>` | File: `src/openMob.Core/Data/AppDbContext.cs` |
| `IProjectPreferenceService` / `ProjectPreferenceService` (nuovi) | Nuovo servizio CRUD per `ProjectPreference` | File: `src/openMob.Core/Services/` |
| `CoreServiceExtensions` | Registrazione DI di `IProjectPreferenceService` | File: `src/openMob.Core/` |

### Dependencies
- `IProviderService.GetProvidersAsync` — già implementato, usato da `ModelPickerViewModel.LoadModelsCommand`
- `IAppPopupService` — già usato da altri picker; deve supportare l'apertura di `ModelPickerSheet` con parametri di callback
- EF Core 9.x + SQLite — già configurato nel progetto
- CommunityToolkit.Mvvm — già usato per `[ObservableProperty]` e `[RelayCommand]`

---

## Open Questions & Clarifications

| # | Question | Status | Answer / Decision |
|---|----------|--------|-------------------|
| 1 | Il meccanismo di callback preferito è `Action<string>` iniettato alla costruzione del picker VM, oppure `WeakReferenceMessenger` di CommunityToolkit? | Open | Da decidere in Technical Analysis. Preferire `Action<string>` per semplicità e testabilità diretta con NSubstitute; usare `WeakReferenceMessenger` solo se il pattern è già adottato altrove nel progetto. |
| 2 | `ProjectPreference.ProjectId` come PK è sufficiente (un solo record per progetto), oppure serve una PK ULID separata? | Open | Da decidere in Technical Analysis. `ProjectId` come PK è sufficiente dato che la relazione è 1:1 progetto → preferenze. |
| 3 | `IAppPopupService` supporta già il passaggio di parametri (es. callback) all'apertura di un popup, oppure va esteso? | Open | Da verificare nell'implementazione di `MauiPopupService`. |

---

## Acceptance Criteria

> Each criterion maps to one or more functional requirements.

- [ ] **[AC-001]** Dato che il server ha provider configurati con modelli, quando l'utente apre il `ModelPickerSheet`, allora vede la lista dinamica raggruppata per provider con i nomi e le dimensioni di contesto reali — non la lista hardcodata. *(REQ-001, REQ-002)*

- [ ] **[AC-002]** Dato che nessun provider ha modelli disponibili (o nessuna chiave API è configurata), quando l'utente apre il picker, allora vede il messaggio "Nessun modello disponibile" e un pulsante che lo porta alle impostazioni provider. *(REQ-003)*

- [ ] **[AC-003]** Dato che l'utente seleziona un modello dal `ModelPickerSheet` aperto dalla `ProjectDetailPage`, quando il picker si chiude, allora `DefaultModelName` nella sezione "DEFAULTS" è aggiornato e il `DefaultModelId` è persistito in SQLite nella tabella `ProjectPreference`. *(REQ-004, REQ-005, REQ-006, REQ-007)*

- [ ] **[AC-004]** Dato che un progetto ha un `DefaultModelId` salvato in SQLite, quando l'utente apre la `ChatPage` per quel progetto, allora la topbar mostra il nome del modello di default (es. "claude-sonnet-4-5") senza che l'utente debba selezionarlo manualmente. *(REQ-008, REQ-010)*

- [ ] **[AC-005]** Dato che l'utente seleziona "Change model" dal More Menu in `ChatPage` e sceglie un modello, quando il picker si chiude, allora la topbar si aggiorna con il nuovo nome in-memory — senza modificare il `DefaultModelId` del progetto in SQLite. *(REQ-009, REQ-010)*

- [ ] **[AC-006]** Dato che `SelectedModelId` su `ChatViewModel` è valorizzato, allora il suo formato è `"providerId/modelId"` e `SelectedModelId.Split('/', 2)` restituisce esattamente due elementi non vuoti. *(REQ-011)*

- [ ] **[AC-007]** Dato che nessun modello è selezionato (`SelectedModelId` è null), allora la topbar mostra il placeholder "Seleziona modello". *(REQ-010)*

---

## Notes for Technical Analysis

> This section is addressed to the agent that will perform the technical implementation analysis.

### Key areas to investigate
- **`IAppPopupService` / `MauiPopupService`:** Verificare se `PopupAsync` / `ShowPopupAsync` supporta già il passaggio di parametri al ViewModel del popup (es. callback `Action<string>`). Se non supportato, valutare l'estensione dell'interfaccia con un overload tipizzato oppure l'uso di `WeakReferenceMessenger`.
- **`ModelPickerViewModel` costruttore:** Valutare l'aggiunta di un parametro opzionale `Action<string>? onModelSelected` per il callback. Questo mantiene la testabilità: nei test si può passare un'action mock senza dipendenze da MAUI.
- **`ProjectDetailViewModel`:** Aggiungere `IProjectPreferenceService` come dipendenza nel costruttore. In `LoadProjectAsync`, caricare anche `ProjectPreference` per pre-popolare `DefaultModelName`.
- **`ChatViewModel`:** Aggiungere `IProjectPreferenceService` come dipendenza. In `LoadContextAsync`, dopo aver ottenuto `_currentProjectId`, chiamare `GetAsync` e impostare `SelectedModelId` / `SelectedModelName`. Esporre `SelectedModelId` come proprietà pubblica (non solo osservabile) per uso futuro dalla send command (Spec 04).
- **Split `"providerId/modelId"`:** Il formato è già usato in `ModelPickerViewModel.ExtractModelsFromProvider`. Verificare che non esistano model ID contenenti `/` (che romperebbero lo split con `maxCount: 2`). Se necessario, usare `IndexOf('/')` per separare solo il primo segmento.

### Suggested implementation approach
1. Creare `ProjectPreference` entity + migrazione EF Core
2. Creare `IProjectPreferenceService` + `ProjectPreferenceService` + registrazione DI
3. Estendere `ModelPickerViewModel` con callback `Action<string>?`
4. Implementare `ProjectDetailViewModel.ChangeModelAsync` (apertura picker + callback + persistenza)
5. Estendere `ChatViewModel` con `SelectedModelId`, `SelectedModelName`, caricamento da DB e gestione "Change model"
6. Refactor `ModelPickerSheet.xaml` (binding dinamici + stato vuoto)
7. Aggiornare `ChatPage.xaml` topbar binding

### Constraints to respect
- `openMob.Core` deve avere zero dipendenze MAUI — il callback `Action<string>` è preferibile a qualsiasi API MAUI-specifica
- Nessuna logica di business nel code-behind XAML
- `async void` solo per handler MAUI lifecycle (`OnAppearing`)
- `ConfigureAwait(false)` in tutti i metodi di `ProjectPreferenceService`
- La migrazione EF Core deve essere generata con `dotnet ef migrations add` — non scritta a mano

### Related files or modules
- `src/openMob.Core/ViewModels/ModelPickerViewModel.cs` — da estendere con callback
- `src/openMob.Core/ViewModels/ProjectDetailViewModel.cs` — da estendere con `IProjectPreferenceService`
- `src/openMob.Core/ViewModels/ChatViewModel.cs` — da estendere con `SelectedModelId`, `SelectedModelName`
- `src/openMob.Core/Data/AppDbContext.cs` — aggiungere `DbSet<ProjectPreference>`
- `src/openMob.Core/Data/Entities/` — nuova entità `ProjectPreference`
- `src/openMob.Core/Services/IProviderService.cs` — già implementato, nessuna modifica
- `src/openMob/Views/Popups/ModelPickerSheet.xaml` + `.xaml.cs` — refactor XAML
- `src/openMob/Views/Pages/ChatPage.xaml` — aggiornare binding topbar
- `src/openMob/Views/Pages/ProjectDetailPage.xaml` — nessuna modifica prevista (binding già presenti)
