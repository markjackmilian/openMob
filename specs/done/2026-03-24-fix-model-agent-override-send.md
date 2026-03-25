# Fix: Model & Agent Override Not Applied on Message Send

## Metadata
| Field       | Value                                        |
|-------------|----------------------------------------------|
| Date        | 2026-03-24                                   |
| Status      | **Completed**                                |
| Version     | 1.0                                          |
| Completed   | 2026-03-25                                   |
| Branch      | bugfix/fix-model-agent-override-send (merged)|
| Merged into | develop                                      |

---

## Executive Summary

When the user selects a specific model or agent in the `MessageComposerSheet` before sending a message, the selection is silently ignored: the model falls back to the project default and the agent is never transmitted to the opencode server at all. This bug makes the per-message model/agent override feature completely non-functional, causing user confusion and incorrect AI behaviour.

---

## Scope

### In Scope
- Fix: il campo `agentName` (wire name: `"agent"`) deve essere aggiunto al `SendPromptRequest` e serializzato nel body della chiamata `POST /session/{id}/prompt_async`.
- Fix: `IChatService.SendPromptAsync` deve accettare e propagare il parametro `agentName`.
- Fix: `ChatViewModel.SendMessageAsync` deve leggere `SelectedAgentName` e passarlo al service.
- Fix: `SendPromptRequestBuilder.FromText` deve accettare e includere il parametro `agentName`.
- Fix: verificare che il flusso Path B (composer → `HandleMessageComposedAsync`) applichi i valori override a `SelectedModelId` e `SelectedAgentName` **prima** dell'esecuzione di `SendMessageCommand`.
- Aggiornamento dei test unitari esistenti e aggiunta di nuovi test per coprire i casi corretti.

### Out of Scope
- Refactoring dello scope di `ProjectPreference` da project-level a session-level.
- Persistenza per-session del modello o dell'agente sul server opencode.
- Bug di `CreateSessionRequest` con body null (issue separata).
- Modifiche all'UI del `MessageComposerSheet` o del `ContextSheet`.
- Gestione del campo `agentName` nel flusso Path A (input bar inline senza composer) — il Path A non espone selezione di agente, quindi non è impattato.

---

## Functional Requirements

> Requirements are numbered for traceability.

1. **[REQ-001]** Il record `SendPromptRequest` deve includere un campo opzionale `Agent` di tipo `string?`, serializzato con `JsonPropertyName("agent")`, posizionato dopo `ProviderId`.

2. **[REQ-002]** Il metodo `SendPromptRequestBuilder.FromText` deve accettare un parametro opzionale `agentName` di tipo `string?` (default `null`) e includerlo nel `SendPromptRequest` costruito.

3. **[REQ-003]** L'interfaccia `IChatService` e la sua implementazione `ChatService` devono aggiornare la firma di `SendPromptAsync` per accettare un parametro `agentName` di tipo `string?` (default `null`) e trasmetterlo al `SendPromptRequestBuilder`.

4. **[REQ-004]** `ChatViewModel.SendMessageAsync` deve leggere `SelectedAgentName` e passarlo come `agentName` alla chiamata `_chatService.SendPromptAsync(...)`.

5. **[REQ-005]** In `ChatViewModel.HandleMessageComposedAsync`, l'aggiornamento di `SelectedAgentName` e `SelectedModelId` dai valori override del messaggio deve avvenire in modo sincrono e completarsi **prima** che `SendMessageCommand.ExecuteAsync(null)` venga invocato. Il codice attuale deve essere verificato per assicurare che non vi siano race condition o await mancanti che causino l'esecuzione del send con i valori precedenti.

6. **[REQ-006]** Se `SelectedAgentName` è `null` (agente di default del progetto), il campo `"agent"` nel body JSON deve essere omesso (non serializzato come `null`), per non sovrascrivere la configurazione server-side. Usare `JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)`.

7. **[REQ-007]** Se `SelectedModelId` è `null`, i campi `"modelID"` e `"providerID"` nel body JSON devono continuare ad essere omessi (comportamento già esistente, da preservare).

---

## Functional Impacts

### Affected Components / Systems
| Component | Impact | Notes |
|-----------|--------|-------|
| `SendPromptRequest.cs` | Modifica — aggiunta campo `Agent` | Aggiungere `JsonIgnore(WhenWritingNull)` |
| `SendPromptRequestBuilder.cs` | Modifica — aggiunta parametro `agentName` | Parametro opzionale, default `null` |
| `IChatService.cs` | Modifica — firma `SendPromptAsync` | Aggiungere `string? agentName = null` |
| `ChatService.cs` | Modifica — implementazione `SendPromptAsync` | Propagare `agentName` al builder |
| `ChatViewModel.cs` | Modifica — `SendMessageAsync` | Leggere e passare `SelectedAgentName` |
| `ChatViewModel.cs` | Verifica — `HandleMessageComposedAsync` | Controllare ordine di esecuzione override → send |
| `SendPromptRequestBuilderTests.cs` | Aggiornamento test esistenti + nuovi casi | Coprire `agentName` presente e assente |
| `ChatViewModelTests.cs` (se esistente) | Aggiornamento/aggiunta test | Verificare che `agentName` venga passato al service |
| `ChatServiceTests.cs` (se esistente) | Aggiornamento/aggiunta test | Verificare che `agentName` venga incluso nel request |

### Dependencies
- Il nome del campo wire `"agent"` è confermato dal `CommandDto` esistente nel progetto (`[property: JsonPropertyName("agent")] string? Agent`), che mappa lo stesso campo del protocollo opencode.
- Nessuna dipendenza su modifiche server-side: il server opencode già supporta il campo `"agent"` nel body del prompt (evidenza: `CommandDto` e `ConfigDto`).

---

## Open Questions & Clarifications

| # | Question | Status | Answer / Decision |
|---|----------|--------|-------------------|
| 1 | Il server opencode accetta il campo `"agent"` nel body di `POST /session/{id}/prompt_async` con la stessa semantica di `CommandDto`? | Resolved | Sì — il campo `"agent"` è già presente nel protocollo opencode (confermato da `CommandDto` e `ConfigDto` nel codebase). Il wire name è `"agent"`. |
| 2 | Il Path A (input bar inline) deve supportare la selezione dell'agente? | Resolved — Out of Scope | Il Path A non espone UI per selezionare l'agente. `SelectedAgentName` in `ChatViewModel` è comunque disponibile e verrà passato anche dal Path A dopo il fix di REQ-004, usando il valore corrente del progetto. |
| 3 | `HandleMessageComposedAsync` ha una race condition sull'aggiornamento di `SelectedAgentName`/`SelectedModelId` prima del send? | Resolved | Da verificare durante l'implementazione (REQ-005). Il codice attuale aggiorna le proprietà in modo sincrono prima di chiamare `SendMessageCommand.ExecuteAsync`, ma la chiamata `_preferenceService.SetAgentAsync` è fire-and-forget — questo è accettabile. |

---

## Acceptance Criteria

> Each criterion maps to one or more functional requirements.

- [ ] **[AC-001]** Dato che l'utente seleziona il modello `"anthropic/claude-opus-4"` nel `MessageComposerSheet` e invia un messaggio, allora il body della chiamata `POST /session/{id}/prompt_async` contiene `"modelID": "claude-opus-4"` e `"providerID": "anthropic"`, e non il modello di default del progetto. *(REQ-002, REQ-003, REQ-004)*

- [ ] **[AC-002]** Dato che l'utente seleziona l'agente `"om-mobile-core"` nel `MessageComposerSheet` e invia un messaggio, allora il body della chiamata `POST /session/{id}/prompt_async` contiene `"agent": "om-mobile-core"`. *(REQ-001, REQ-002, REQ-003, REQ-004)*

- [ ] **[AC-003]** Dato che l'utente non seleziona alcun agente (agente di default), allora il body della chiamata `POST /session/{id}/prompt_async` **non contiene** il campo `"agent"` (omesso, non `null`). *(REQ-006)*

- [ ] **[AC-004]** Il test unitario `SendPromptRequestBuilder_WhenAgentNameProvided_IncludesAgentField` verifica che `FromText("text", agentName: "om-mobile-core")` produca un `SendPromptRequest` con `Agent == "om-mobile-core"`. *(REQ-002)*

- [ ] **[AC-005]** Il test unitario `SendPromptRequestBuilder_WhenAgentNameIsNull_OmitsAgentField` verifica che il JSON serializzato di `SendPromptRequest` con `Agent = null` non contenga la chiave `"agent"`. *(REQ-006)*

- [ ] **[AC-006]** Il test unitario su `ChatViewModel` verifica che `SendMessageAsync` chiami `IChatService.SendPromptAsync` con il valore corretto di `agentName` preso da `SelectedAgentName`. *(REQ-004)*

- [ ] **[AC-007]** Il test unitario su `ChatViewModel` verifica che dopo `HandleMessageComposedAsync` con un `AgentOverride` diverso, la successiva chiamata al service usi il valore override e non quello precedente. *(REQ-005)*

- [ ] **[AC-008]** La build dell'intera solution (`dotnet build openMob.sln`) termina con exit code 0 e zero warning dopo le modifiche.

- [ ] **[AC-009]** Tutti i test esistenti passano senza regressioni (`dotnet test tests/openMob.Tests/openMob.Tests.csproj`).

---

## Notes for Technical Analysis

> This section is addressed to the agent that will perform the technical implementation analysis.

- **Root cause confermato**: `SendPromptRequest` non ha il campo `Agent`; `IChatService.SendPromptAsync` non accetta `agentName`; `ChatViewModel.SendMessageAsync` non legge `SelectedAgentName`. La catena di propagazione è interrotta in tre punti.

- **Wire name dell'agente**: usare `"agent"` (minuscolo), coerente con `CommandDto` (`[property: JsonPropertyName("agent")] string? Agent`). Non usare `"agentName"` o `"agentID"`.

- **Serializzazione null**: aggiungere `[property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]` al campo `Agent` in `SendPromptRequest`, così come già dovrebbe essere per `ModelId` e `ProviderId` (verificare che anche questi abbiano la stessa annotation, altrimenti aggiungerla per coerenza).

- **Ordine delle modifiche suggerito** (bottom-up per evitare errori di compilazione intermedi):
  1. `SendPromptRequest.cs` — aggiungere campo `Agent`
  2. `SendPromptRequestBuilder.cs` — aggiungere parametro `agentName`
  3. `IChatService.cs` + `ChatService.cs` — aggiornare firma e implementazione
  4. `ChatViewModel.cs` — aggiornare `SendMessageAsync` (REQ-004) e verificare `HandleMessageComposedAsync` (REQ-005)
  5. Test — aggiornare `SendPromptRequestBuilderTests.cs` e aggiungere test su ViewModel/Service

- **File chiave da modificare**:
  - `src/openMob.Core/Infrastructure/Http/Dtos/Opencode/Requests/SendPromptRequest.cs`
  - `src/openMob.Core/Helpers/SendPromptRequestBuilder.cs`
  - `src/openMob.Core/Services/IChatService.cs`
  - `src/openMob.Core/Services/ChatService.cs`
  - `src/openMob.Core/ViewModels/ChatViewModel.cs` (metodi `SendMessageAsync` e `HandleMessageComposedAsync`)
  - `tests/openMob.Tests/Helpers/SendPromptRequestBuilderTests.cs`

- **Verifica `HandleMessageComposedAsync`**: il codice attuale (linee 799–809 di `ChatViewModel.cs`) aggiorna `SelectedAgentName` e poi chiama `SendMessageCommand.ExecuteAsync(null)`. Poiché `SelectedAgentName` è una proprietà sincrona e `SendMessageAsync` la legge all'inizio della sua esecuzione, non dovrebbe esserci race condition — ma verificare che non ci siano `await` intermedi che potrebbero causare un context switch prima della lettura.

- **Nessuna modifica server-side necessaria**: il server opencode già supporta il campo `"agent"` nel body del prompt.

- **Attenzione**: `MessageComposerViewModel` invia `AgentOverride` tramite `MessageComposedMessage`. `ChatViewModel.HandleMessageComposedAsync` riceve il messaggio e aggiorna `SelectedAgentName`. Assicurarsi che questo aggiornamento avvenga prima della chiamata al service, non dopo.

---

## Technical Analysis

> Added by: om-orchestrator | Date: 2026-03-24

### Change Classification

| Field | Value |
|-------|-------|
| Change type | Bug Fix |
| Git Flow branch | bugfix/fix-model-agent-override-send |
| Branches from | develop |
| Estimated complexity | Low |
| Estimated agents involved | om-mobile-core, om-tester, om-reviewer |

### Layers Involved

| Layer | Agent | Scope |
|-------|-------|-------|
| DTO / Request model | om-mobile-core | `src/openMob.Core/Infrastructure/Http/Dtos/Opencode/Requests/SendPromptRequest.cs` |
| Helper / Builder | om-mobile-core | `src/openMob.Core/Helpers/SendPromptRequestBuilder.cs` |
| Service interface | om-mobile-core | `src/openMob.Core/Services/IChatService.cs` |
| Service implementation | om-mobile-core | `src/openMob.Core/Services/ChatService.cs` |
| ViewModel | om-mobile-core | `src/openMob.Core/ViewModels/ChatViewModel.cs` |
| Unit Tests | om-tester | `tests/openMob.Tests/Helpers/SendPromptRequestBuilderTests.cs`, `tests/openMob.Tests/ViewModels/ChatViewModelTests.cs` |
| Code Review | om-reviewer | all of the above |

### Files to Create

- None — this is a pure bug fix with no new files required.

### Files to Modify

- `src/openMob.Core/Infrastructure/Http/Dtos/Opencode/Requests/SendPromptRequest.cs` — add optional `Agent` field with `JsonPropertyName("agent")` and `JsonIgnore(WhenWritingNull)`; also verify `ModelId` and `ProviderId` have `JsonIgnore(WhenWritingNull)` (currently missing — add for consistency with REQ-007)
- `src/openMob.Core/Helpers/SendPromptRequestBuilder.cs` — add optional `agentName` parameter to `FromText`, pass it to `SendPromptRequest` constructor
- `src/openMob.Core/Services/IChatService.cs` — add `string? agentName = null` parameter to `SendPromptAsync` signature
- `src/openMob.Core/Services/ChatService.cs` — add `string? agentName = null` parameter to `SendPromptAsync` implementation, pass it to `SendPromptRequestBuilder.FromText`
- `src/openMob.Core/ViewModels/ChatViewModel.cs` — in `SendMessageAsync`, read `SelectedAgentName` and pass it as `agentName` to `_chatService.SendPromptAsync`; verify `HandleMessageComposedAsync` ordering (REQ-005)
- `tests/openMob.Tests/Helpers/SendPromptRequestBuilderTests.cs` — add tests for `agentName` present and absent (AC-004, AC-005)
- `tests/openMob.Tests/ViewModels/ChatViewModelTests.cs` — update existing `SendPromptAsync` mock setups to include the new `agentName` parameter; add new tests for AC-006 and AC-007

### Technical Dependencies

- No new NuGet packages required.
- No EF Core / SQLite schema changes.
- No server-side changes — the opencode server already supports `"agent"` in the prompt body (confirmed by `CommandDto`).
- The existing `ChatViewModelMessageComposerTests.cs` already tests `HandleMessageComposedAsync` agent override behaviour but does **not** verify that `agentName` is passed to `SendPromptAsync`. The new tests in `ChatViewModelTests.cs` must cover this gap.

### Technical Risks

- **Breaking change on `IChatService.SendPromptAsync` signature**: adding a new optional parameter with a default value (`string? agentName = null`) is source-compatible but callers that use named arguments or positional arguments beyond the 4th parameter must be updated. Inspection shows only `ChatViewModel.SendMessageAsync` calls this method directly — all test mocks use `Arg.Any<string?>()` matchers and will continue to compile. The existing test mock setups in `ChatViewModelTests.cs` and `ChatViewModelMessageComposerTests.cs` use 5-argument `SendPromptAsync` matchers — after adding the 6th parameter, these must be updated to include `Arg.Any<string?>()` for `agentName` or the mocks will not match and tests will fail.
- **`JsonIgnore(WhenWritingNull)` missing on `ModelId`/`ProviderId`**: the current `SendPromptRequest` record does not have `JsonIgnore` on `ModelId` and `ProviderId`. If `null`, these fields will serialize as `"modelID": null` and `"providerID": null`, which may override server-side defaults. Adding `JsonIgnore(WhenWritingNull)` to all three fields is required for REQ-007 correctness.
- **`HandleMessageComposedAsync` ordering (REQ-005)**: code inspection confirms that `SelectedAgentName` and `SelectedModelId` are set synchronously (lines 799–809) before `SendMessageCommand.ExecuteAsync(null)` is called (line 833). No race condition exists. The fix to `SendMessageAsync` (reading `SelectedAgentName`) will correctly pick up the updated value.

### Execution Order

> Steps that can run in parallel are marked with ⟳. Steps that must be sequential are numbered.

1. [Git Flow] Create branch `bugfix/fix-model-agent-override-send`
2. [om-mobile-core] Implement all 5 file changes bottom-up: `SendPromptRequest` → `SendPromptRequestBuilder` → `IChatService` → `ChatService` → `ChatViewModel`
3. [om-tester] Write/update unit tests in `SendPromptRequestBuilderTests.cs` and `ChatViewModelTests.cs`
4. [om-reviewer] Full review against spec
5. [Fix loop if needed] Address Critical and Major findings
6. [Git Flow] Finish branch and merge into develop

### Definition of Done

- [ ] All `[REQ-001]` through `[REQ-007]` requirements implemented
- [ ] All `[AC-001]` through `[AC-009]` acceptance criteria satisfied
- [ ] Unit tests written for AC-004, AC-005, AC-006, AC-007
- [ ] All existing tests pass without regression (AC-009)
- [ ] `om-reviewer` verdict: ✅ Approved or ⚠️ Approved with remarks
- [ ] Git Flow branch finished and deleted
- [ ] Spec moved to `specs/done/` with Completed status
