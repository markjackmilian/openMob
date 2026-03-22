# SSE Project Directory Propagation

## Metadata
| Field       | Value                                              |
|-------------|----------------------------------------------------|
| Date        | 2026-03-21                                         |
| Status      | **Completed**                                      |
| Version     | 1.0                                                |
| Completed   | 2026-03-22                                         |
| Branch      | feature/sse-project-directory-propagation (merged)  |
| Merged into | develop                                            |

---

## Executive Summary

Il server opencode emette eventi SSE con un envelope che include il campo `directory` — il path assoluto del progetto a cui appartiene l'evento. Attualmente `ChatEventParser` legge questo campo ma lo scarta: nessun tipo `ChatEvent` lo porta, e i consumer (in primis `ChatViewModel`) non possono filtrare per progetto in modo esplicito. Questa feature propaga `ProjectDirectory` attraverso tutta la pipeline SSE — dal parser ai tipi `ChatEvent` fino agli handler di `ChatViewModel` — rendendo il routing multi-progetto esplicito, robusto e pronto per futuri consumer.

---

## Scope

### In Scope
- Estrazione del campo `directory` dall'envelope SSE in `ChatEventParser` e sua propagazione come `string? ProjectDirectory` nei tipi `ChatEvent`
- Aggiunta di `string? ProjectDirectory` come proprietà comune al record base `ChatEvent` (o ai tipi derivati che la necessitano)
- Aggiunta di un secondo filtro esplicito per `ProjectDirectory` negli handler SSE di `ChatViewModel`
- Nessuna modifica all'interfaccia `IChatService` né al loop di riconnessione in `ChatService`
- Aggiornamento dei test esistenti per i tipi `ChatEvent` e per `ChatEventParser`
- Nuovi test per il filtraggio per `ProjectDirectory` in `ChatViewModel`

### Out of Scope
- Rimozione o consolidamento del metodo `IOpencodeApiClient.SubscribeToEventsAsync` (duplicazione SSE — da trattare in una feature separata)
- Consumo di eventi SSE da parte di ViewModel diversi da `ChatViewModel` (es. `FlyoutViewModel`) — questa feature prepara l'infrastruttura, non i consumer futuri
- Modifiche all'UI
- Modifiche al loop di riconnessione SSE in `ChatService`
- Modifiche alla gestione del cambio di server attivo

---

## Functional Requirements

> Requirements numerati per tracciabilità.

1. **[REQ-001]** `ChatEventParser.Parse()` deve estrarre il campo `directory` dall'envelope SSE (`root.GetProperty("directory")`) e propagarlo come `string? ProjectDirectory` nel `ChatEvent` risultante. Se il campo è assente o vuoto, `ProjectDirectory` è `null`.

2. **[REQ-002]** Il record base `ChatEvent` (o un'interfaccia comune) deve esporre la proprietà `string? ProjectDirectory`. Tutti i tipi derivati concreti (`MessageUpdatedEvent`, `MessagePartDeltaEvent`, `MessagePartUpdatedEvent`, `SessionUpdatedEvent`, `SessionErrorEvent`, `ServerConnectedEvent`, `PermissionRequestedEvent`, `PermissionUpdatedEvent`, `UnknownEvent`) devono includere questa proprietà.

3. **[REQ-003]** Gli handler SSE in `ChatViewModel` (`HandleMessageUpdated`, `HandleMessagePartDelta`, `HandleMessagePartUpdated`, `HandleSessionUpdated`, `HandleSessionError`) devono aggiungere un secondo filtro: se `e.ProjectDirectory` è non-null e non corrisponde al `CurrentProjectDirectory` del ViewModel, l'evento viene scartato silenziosamente.

4. **[REQ-004]** `ChatViewModel` deve esporre una proprietà `CurrentProjectDirectory` (o equivalente) che rappresenta il path del progetto corrente, ricavato da `IActiveProjectService.GetCachedWorktree()` al momento del caricamento del contesto (`LoadContextAsync`).

5. **[REQ-005]** Il filtraggio per `ProjectDirectory` è **aggiuntivo** rispetto al filtraggio per `sessionId` già esistente: entrambi i filtri devono essere soddisfatti. L'ordine di valutazione è: prima `ProjectDirectory` (se non-null), poi `sessionId`.

6. **[REQ-006]** Se `ProjectDirectory` è `null` nell'evento SSE (campo assente nel wire format), il filtro per `ProjectDirectory` viene saltato e si applica solo il filtro per `sessionId`. Questo garantisce retrocompatibilità con eventuali versioni del server che non emettono il campo.

7. **[REQ-007]** `ChatEventParser` deve gestire l'assenza del campo `directory` senza lanciare eccezioni: usare `TryGetProperty` o equivalente con fallback a `null`.

8. **[REQ-008]** I test esistenti per `ChatEventParser` e per i tipi `ChatEvent` devono continuare a passare. I test che costruiscono `ChatEvent` direttamente devono essere aggiornati per includere `ProjectDirectory = null` (o il valore appropriato) nei costruttori dei record.

9. **[REQ-009]** Nuovi test unitari per `ChatViewModel` devono verificare che:
   - Un evento con `ProjectDirectory` corrispondente al progetto corrente viene processato.
   - Un evento con `ProjectDirectory` diverso dal progetto corrente viene scartato.
   - Un evento con `ProjectDirectory = null` viene processato (filtro saltato, solo `sessionId` applicato).

---

## Functional Impacts

### Affected Components / Systems

| Component | Impact | Notes |
|-----------|--------|-------|
| `src/openMob.Core/Helpers/ChatEventParser.cs` | Modifica | Estrarre `directory` dall'envelope e passarlo ai tipi `ChatEvent` |
| `src/openMob.Core/Models/ChatEvent.cs` | Modifica | Aggiungere `string? ProjectDirectory` alla base `abstract record` |
| `src/openMob.Core/ViewModels/ChatViewModel.cs` | Modifica | Aggiungere `CurrentProjectDirectory`, secondo filtro negli handler |
| `tests/openMob.Tests/Helpers/ChatEventParserTests.cs` | Modifica + nuovi test | Verificare propagazione `ProjectDirectory` |
| `tests/openMob.Tests/ViewModels/ChatViewModelSseTests.cs` | Nuovi test | Filtraggio per `ProjectDirectory` |
| Tutti i test che costruiscono `ChatEvent` direttamente | Modifica | Aggiornare costruttori dei record |

### Dependencies
- `IActiveProjectService.GetCachedWorktree()` — già esistente (introdotto in `feature/new-session-drawer-button`). Fornisce il path del progetto corrente in modo sincrono dalla cache in memoria.
- Il campo `directory` nel wire format SSE è il medesimo path iniettato dall'app via header `x-opencode-directory` (confermato da ADR `adr-global-directory-header-injection`). La mappatura è diretta: `directory` SSE == `IActiveProjectService.GetCachedWorktree()`.
- Nessuna nuova dipendenza NuGet.
- Nessuna migrazione EF Core.

---

## Open Questions & Clarifications

| # | Question | Status | Answer / Decision |
|---|----------|--------|-------------------|
| 1 | Il campo `directory` nel wire format SSE corrisponde al path restituito da `IActiveProjectService.GetCachedWorktree()`? | Resolved | Sì. Il server opencode legge il progetto dall'header `x-opencode-directory` (iniettato da `OpencodeApiClient` con il valore di `GetCachedWorktree()`) e lo usa come `directory` nell'envelope SSE. La mappatura è diretta. (ADR: `adr-global-directory-header-injection`) |
| 2 | I `sessionId` opencode sono univoci globalmente tra progetti diversi? | Resolved — non bloccante | Probabile sì (ULID). Il filtraggio per `ProjectDirectory` è comunque aggiunto per rendere il routing esplicito e robusto, indipendentemente dall'unicità dei `sessionId`. |
| 3 | `ProjectDirectory` va sulla base `ChatEvent` o solo sui tipi che ne hanno bisogno? | Resolved | Sulla base `ChatEvent` (proprietà `init`-only con default `null`). Semplifica il codice del parser e degli handler, e garantisce che futuri consumer abbiano sempre il contesto disponibile. |
| 4 | `ChatViewModel` deve iniettare `IActiveProjectService` nel costruttore? | Resolved | Sì, `IActiveProjectService` è **già iniettato** in `ChatViewModel` come `_activeProjectService` (field at line 45, constructor parameter at line 73). Nessuna modifica al costruttore necessaria. |

---

## Acceptance Criteria

> Ogni criterio mappa a uno o più requisiti funzionali.

- [ ] **[AC-001]** Dato un evento SSE con envelope `{ "directory": "/path/to/project", "payload": {...} }`, quando `ChatEventParser.Parse()` lo processa, allora il `ChatEvent` risultante ha `ProjectDirectory == "/path/to/project"`. *(REQ-001, REQ-002)*

- [ ] **[AC-002]** Dato un evento SSE con envelope privo del campo `directory`, quando `ChatEventParser.Parse()` lo processa, allora il `ChatEvent` risultante ha `ProjectDirectory == null` e nessuna eccezione viene lanciata. *(REQ-007)*

- [ ] **[AC-003]** Dato un `ChatViewModel` con `CurrentProjectDirectory == "/proj/A"` e un evento SSE con `ProjectDirectory == "/proj/B"` e `SessionId == CurrentSessionId`, quando l'evento arriva, allora l'handler lo scarta e la `Messages` collection non viene modificata. *(REQ-003, REQ-005)*

- [ ] **[AC-004]** Dato un `ChatViewModel` con `CurrentProjectDirectory == "/proj/A"` e un evento SSE con `ProjectDirectory == "/proj/A"` e `SessionId == CurrentSessionId`, quando l'evento arriva, allora l'handler lo processa normalmente. *(REQ-003)*

- [ ] **[AC-005]** Dato un evento SSE con `ProjectDirectory == null`, quando arriva a `ChatViewModel`, allora il filtro per `ProjectDirectory` viene saltato e si applica solo il filtro per `sessionId`. *(REQ-006)*

- [ ] **[AC-006]** Tutti i test esistenti continuano a passare dopo le modifiche ai tipi `ChatEvent` e a `ChatEventParser`. *(REQ-008)*

- [ ] **[AC-007]** Nuovi test unitari coprono i tre scenari di filtraggio per `ProjectDirectory` (match, mismatch, null). *(REQ-009)*

---

## Notes for Technical Analysis

> Questa sezione è indirizzata all'agente che eseguirà l'analisi tecnica e l'implementazione.

### Contesto architetturale rilevante

- **`ChatEvent` è un record base** con tipi derivati sealed. Aggiungere `string? ProjectDirectory` al record base è il cambiamento minimo. Verificare se `ChatEvent` è un `abstract record` o una `sealed record` — nel primo caso la proprietà va sulla base, nel secondo va su ogni tipo derivato.
- **`ChatEventParser.Parse()`** riceve un `OpencodeEventDto` con `Data` come `JsonElement?`. L'envelope è nel campo `Data`. Usare `data.Value.TryGetProperty("directory", out var dirElement)` per estrarre il campo in modo sicuro.
- **`IActiveProjectService.GetCachedWorktree()`** è sincrono e restituisce `string?` (null se nessun progetto in cache). Introdotto in `feature/new-session-drawer-button` per risolvere il deadlock da chiamata async in `ExecuteAsync`. Stesso pattern applicabile in `ChatViewModel.LoadContextAsync`.
- **`ChatViewModel` è Transient**: `CurrentProjectDirectory` può essere impostato una volta in `LoadContextAsync` e non cambia per tutta la vita del ViewModel (ogni cambio di progetto crea un nuovo `ChatViewModel`).
- **`ConfigureAwait(false)` non deve essere usato nei ViewModel** (regola stabilita in `feature/drawer-sessions-delete-refactor`).
- **Tutti i dispatch alla UI thread** negli handler SSE devono continuare a usare `IDispatcherService.Dispatch(...)`.

### Approccio implementativo suggerito

**Step 1 — `ChatEvent` base record**
```csharp
// Aggiungere a ChatEvent (o al record base comune)
public string? ProjectDirectory { get; init; }
```

**Step 2 — `ChatEventParser.Parse()`**
```csharp
// Estrarre directory dall'envelope prima di fare il dispatch per tipo
string? projectDirectory = null;
if (dto.Data.HasValue &&
    dto.Data.Value.TryGetProperty("directory", out var dirEl) &&
    dirEl.ValueKind == JsonValueKind.String)
{
    projectDirectory = dirEl.GetString();
}

// Passare projectDirectory a ogni tipo costruito:
// return new MessageUpdatedEvent(...) { ProjectDirectory = projectDirectory };
```

**Step 3 — `ChatViewModel`**
```csharp
// Campo
private string? _currentProjectDirectory;

// In LoadContextAsync (dopo aver caricato il progetto attivo):
_currentProjectDirectory = _activeProjectService.GetCachedWorktree();

// In ogni handler SSE, aggiungere PRIMA del filtro sessionId:
if (e.ProjectDirectory is not null &&
    e.ProjectDirectory != _currentProjectDirectory)
    return;
```

### File da ispezionare prima dell'implementazione
- `src/openMob.Core/Models/ChatEvent.cs` — struttura del record base e dei tipi derivati
- `src/openMob.Core/Helpers/ChatEventParser.cs` — logica di parsing attuale
- `src/openMob.Core/ViewModels/ChatViewModel.cs` — costruttore, `LoadContextAsync`, handler SSE
- `src/openMob.Core/Services/IActiveProjectService.cs` — firma di `GetCachedWorktree()`
- `tests/openMob.Tests/Helpers/ChatEventParserTests.cs` — test esistenti da aggiornare

### Vincoli da rispettare
- `ChatService` è Singleton e non va modificato.
- Nessuna modifica a `IChatService` né al loop di riconnessione.
- I tipi `ChatEvent` sono sealed records: l'aggiunta di `ProjectDirectory` con `init` e valore default `null` è retrocompatibile con i costruttori esistenti solo se il record usa la sintassi `{ ProjectDirectory = ... }` — verificare se i test costruiscono i record con sintassi posizionale o nominale.
- Suite corrente: tutti i test devono continuare a passare.
- Complessità stimata: **Bassa**. Nessuna nuova interfaccia, nessun nuovo servizio, nessuna migrazione DB.

---

## Technical Analysis

> Added by: om-orchestrator | Date: 2026-03-22

### Change Classification

| Field | Value |
|-------|-------|
| Change type | Feature |
| Git Flow branch | feature/sse-project-directory-propagation |
| Branches from | develop |
| Estimated complexity | Low |
| Estimated agents involved | om-mobile-core, om-tester, om-reviewer |

### Layers Involved

| Layer | Agent | Scope |
|-------|-------|-------|
| Business logic / Models | om-mobile-core | `src/openMob.Core/Models/ChatEvent.cs` |
| Helpers / Parser | om-mobile-core | `src/openMob.Core/Helpers/ChatEventParser.cs` |
| ViewModels | om-mobile-core | `src/openMob.Core/ViewModels/ChatViewModel.cs` |
| Unit Tests | om-tester | `tests/openMob.Tests/` |
| Code Review | om-reviewer | all of the above |

### Files to Create

- None — this feature only modifies existing files.

### Files to Modify

- `src/openMob.Core/Models/ChatEvent.cs` — Add `string? ProjectDirectory { get; init; }` to the abstract base record `ChatEvent`
- `src/openMob.Core/Helpers/ChatEventParser.cs` — Extract `directory` from SSE envelope before type dispatch; propagate as `ProjectDirectory` on every constructed event
- `src/openMob.Core/ViewModels/ChatViewModel.cs` — Add `_currentProjectDirectory` field, set it in `LoadContextAsync` via `_activeProjectService.GetCachedWorktree()`, add project directory filter before session ID filter in all 5 SSE handlers
- `tests/openMob.Tests/Helpers/ChatEventParserTests.cs` — Add tests for `ProjectDirectory` extraction (present, absent, empty)
- `tests/openMob.Tests/ViewModels/ChatViewModelSseTests.cs` — Add 3 new tests for project directory filtering (match, mismatch, null)
- All test files constructing `ChatEvent` instances — No changes needed because `ProjectDirectory` has `init` with implicit `null` default; existing nominal-syntax constructions (`new MessageUpdatedEvent { Message = ... }`) remain valid

### Technical Dependencies

- `IActiveProjectService.GetCachedWorktree()` — already exists and is already injected into `ChatViewModel`
- `ChatEvent` is an `abstract record` — adding `string? ProjectDirectory { get; init; }` to the base is fully backward-compatible
- All derived types use nominal object-initializer syntax (not positional) — no constructor signature changes needed
- All existing tests use nominal syntax — they will compile without modification

### Technical Risks

- **None identified.** This is a purely additive change with no breaking interfaces, no new dependencies, and no migrations.
- The `ProjectDirectory` property defaults to `null` via `init`, so all existing code that constructs `ChatEvent` instances without setting `ProjectDirectory` will continue to work.

### Execution Order

> Steps that can run in parallel are marked with ⟳. Steps that must be sequential are numbered.

1. [Git Flow] Create branch `feature/sse-project-directory-propagation`
2. [om-mobile-core] Add `ProjectDirectory` to `ChatEvent` base record, update `ChatEventParser`, add filter to `ChatViewModel` SSE handlers
3. [om-tester] Write unit tests for parser propagation and ViewModel filtering
4. [om-reviewer] Full review against spec
5. [Fix loop if needed] Address Critical and Major findings
6. [Git Flow] Finish branch and merge

### Definition of Done

- [ ] All `[REQ-001]` through `[REQ-009]` requirements implemented
- [ ] All `[AC-001]` through `[AC-007]` acceptance criteria satisfied
- [ ] Unit tests written for ChatEventParser (ProjectDirectory propagation) and ChatViewModel (directory filtering)
- [ ] `om-reviewer` verdict: Approved or Approved with remarks
- [ ] Git Flow branch finished and deleted
- [ ] Spec moved to `specs/done/` with Completed status
