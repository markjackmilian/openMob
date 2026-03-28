# SSE Message Content Fix — Diagnosi e Correzione del Parsing degli Eventi Real-Time

## Metadata
| Field       | Value                                              |
|-------------|---------------------------------------------------|
| Date        | 2026-03-19                                        |
| Status      | **Completed**                                     |
| Version     | 1.0                                               |
| Completed   | 2026-03-28                                        |
| Branch      | bugfix/fix-sse-real-time-streaming (merged)       |
| Merged into | develop                                           |

---

## Executive Summary

La pagina chat riceve correttamente gli eventi SSE dal server opencode ma non riesce a visualizzare il contenuto testuale delle risposte dell'assistente: la riga di risposta appare nella UI ma rimane vuota. Il problema risiede con alta probabilità in un disallineamento tra la struttura JSON dell'envelope SSE attesa dal `ChatEventParser` e quella realmente inviata dal server. Questa spec descrive il processo di diagnosi tramite log `#if DEBUG` già introdotti, la correzione del parser e del ViewModel in base ai dati reali osservati, e la verifica end-to-end del flusso di streaming.

---

## Scope

### In Scope
- Diagnosi della struttura JSON reale degli eventi SSE tramite log `adb logcat` (tag `[SSE_RAW]` e `[SSE_PARSER]`)
- Correzione di `ChatEventParser` per allinearsi alla struttura wire reale del server opencode
- Correzione dell'endpoint SSE se errato (`/global/event` vs `/event`)
- Correzione del `ChatViewModel` se il problema è nel threading o nel wiring ViewModel→UI
- Correzione dei DTO (`PartDto`, `MessageWithPartsDto`) se i nomi dei campi JSON non corrispondono
- Verifica che il testo della risposta dell'assistente appaia in streaming nella UI

### Out of Scope
- Visualizzazione di tipi di messaggi non testuali (`ToolPart`, `FilePart`, `StepStartPart`, ecc.)
- Gestione dei permessi (`permission.requested`, `permission.updated`)
- Reconnect automatico SSE (già implementato in `ChatService`)
- Modifiche alla UI della chat (già completata in `feature/chat-ui-completion`)

---

## Functional Requirements

> Requirements are numbered for traceability.

1. **[REQ-001]** Il `ChatEventParser` deve parsare correttamente gli eventi SSE nella struttura JSON reale inviata dal server opencode, indipendentemente dal fatto che l'envelope usi un wrapper `payload` o una struttura piatta.

2. **[REQ-002]** L'endpoint SSE usato da `OpencodeApiClient.SubscribeToEventsAsync` deve corrispondere all'endpoint reale del server opencode. Se il server espone `/event` anziché `/global/event`, l'endpoint deve essere corretto.

3. **[REQ-003]** Quando il server invia un evento `message.part.updated` con `type: "text"` e un campo testo non vuoto, il `ChatViewModel` deve aggiornare `TextContent` del `ChatMessage` corrispondente e la UI deve mostrare il testo.

4. **[REQ-004]** Quando il server invia un evento `message.part.delta` con `field: "text"` e un `delta` non vuoto, il `ChatViewModel` deve appendere il delta a `TextContent` del `ChatMessage` corrispondente in modo incrementale (streaming).

5. **[REQ-005]** Quando il server invia un evento `message.updated` con un `AssistantMessage` che contiene parti di tipo `text`, il `ChatViewModel` deve estrarre il testo e aggiornare `TextContent` del `ChatMessage` corrispondente.

6. **[REQ-006]** Tutti gli aggiornamenti a proprietà osservabili del `ChatViewModel` che avvengono fuori dal thread UI devono essere eseguiti tramite `IDispatcherService.RunOnMainThreadAsync` per evitare eccezioni di cross-thread.

7. **[REQ-007]** I DTO (`PartDto`, `MessageWithPartsDto`, `MessageInfoDto`) devono avere attributi `[JsonPropertyName]` che corrispondono esattamente ai nomi camelCase dei campi JSON inviati dal server. Se i nomi attuali non corrispondono, devono essere corretti.

8. **[REQ-008]** Gli eventi SSE non riconosciuti o non parsabili devono continuare a essere silenziosamente ignorati (restituiti come `UnknownEvent`) senza interrompere lo stream.

9. **[REQ-009]** I log `#if DEBUG` introdotti in `OpencodeApiClient` e `ChatEventParser` devono rimanere nel codice per facilitare future diagnosi. Non devono essere rimossi.

---

## Functional Impacts

### Affected Components / Systems
| Component | Impact | Notes |
|-----------|--------|-------|
| `OpencodeApiClient.cs` | Possibile correzione endpoint (`/global/event` → `/event`) | Da verificare con i log |
| `ChatEventParser.cs` | Correzione della logica di unwrapping dell'envelope JSON | Il bug principale atteso |
| `PartDto.cs` / `MessageDtos.cs` | Possibile correzione di `[JsonPropertyName]` | Da verificare con i log |
| `ChatViewModel.cs` | Possibile correzione del threading o del wiring degli handler | Da verificare |
| `ChatMessage.cs` | Nessuna modifica attesa | Solo lettura |

### Dependencies
- Log `#if DEBUG` già introdotti in `OpencodeApiClient.cs` e `ChatEventParser.cs` (prerequisito soddisfatto)
- Server opencode in esecuzione e raggiungibile dal dispositivo Android durante il test
- `adb logcat` disponibile per leggere i log durante una sessione di chat reale

---

## Open Questions & Clarifications

| # | Question | Status | Answer / Decision |
|---|----------|--------|-------------------|
| 1 | L'envelope SSE reale usa il wrapper `{ "payload": { "type": ..., "properties": ... } }` o una struttura piatta `{ "type": ..., "properties": ... }`? | **Resolved** | Confermato: il server usa il wrapper `payload`. Il `ChatEventParser` è già corretto. |
| 2 | L'endpoint corretto è `/event` o `/global/event`? | **Open** | Non verificabile senza `adb logcat`. Il codice mantiene `/global/event` per ora; i log `[SSE_RAW]` permetteranno di determinarlo a runtime. |
| 3 | I nomi dei campi JSON in `PartDto` (`sessionID`, `messageID`, `partID`) corrispondono a quelli reali? | **Resolved** | Confermato: `sessionID`, `messageID` sono corretti. `partID` non è un campo di `PartDto` (il campo è `id`). |
| 4 | Gli aggiornamenti al `ChatMessage` avvengono sul thread corretto? | **Resolved** | Tutti gli handler SSE in `ChatViewModel` usano `_dispatcher.Dispatch(...)`. |

---

## Acceptance Criteria

> Each criterion maps to one or more functional requirements.

- [ ] **[AC-001]** Dato un server opencode attivo e una sessione di chat aperta, quando l'utente invia un prompt, allora il log `adb logcat` mostra eventi `[SSE_RAW]` con JSON non vuoto. *(REQ-002)*

- [ ] **[AC-002]** Dato un evento SSE ricevuto, quando il log `[SSE_PARSER] parsed as` mostra `MessagePartUpdatedEvent` o `MessagePartDeltaEvent` (e non `UnknownEvent`), allora il parser sta leggendo correttamente la struttura dell'envelope. *(REQ-001)*

- [ ] **[AC-003]** Dato un evento `message.part.updated` con `type: "text"` e testo non vuoto, quando l'evento viene processato dal `ChatViewModel`, allora la bolla di risposta dell'assistente nella UI mostra il testo ricevuto. *(REQ-003)*

- [ ] **[AC-004]** Dato un evento `message.part.delta` con `field: "text"`, quando l'evento viene processato, allora il testo nella bolla dell'assistente cresce incrementalmente (effetto streaming). *(REQ-004)*

- [ ] **[AC-005]** Dato un evento `message.updated` con parti testuali, quando l'evento viene processato, allora `ChatMessage.TextContent` viene aggiornato con il testo completo. *(REQ-005)*

- [ ] **[AC-006]** Nessuna eccezione `InvalidOperationException` di cross-thread viene lanciata durante la ricezione di eventi SSE. *(REQ-006)*

- [ ] **[AC-007]** La build dell'intera soluzione (`dotnet build openMob.sln`) termina con exit code 0 e zero warning. *(tutti)*

- [ ] **[AC-008]** I test esistenti in `openMob.Tests` passano senza regressioni. *(tutti)*

---

## Notes for Technical Analysis

> This section is addressed to the agent that will perform the technical implementation analysis.

### Processo di diagnosi (da eseguire prima di scrivere codice)

L'agente deve seguire questo processo in ordine:

**Step 1 — Verificare che gli eventi arrivino fisicamente**
```
adb logcat | grep "\[SSE_RAW\]"
```
- Se non appare nulla → l'endpoint è sbagliato. Correggere `/global/event` → `/event` in `OpencodeApiClient.SubscribeToEventsAsync` (riga ~636).
- Se appaiono righe → procedere al Step 2.

**Step 2 — Leggere la struttura JSON reale dell'envelope**
```
adb logcat | grep "\[SSE_PARSER\] raw envelope"
```
Confrontare con la struttura attesa dal parser:
```json
{ "directory": "...", "payload": { "type": "...", "properties": { ... } } }
```
Se la struttura è diversa (es. `{ "type": "...", "properties": {...} }` senza `payload`), correggere `ChatEventParser.Parse()` di conseguenza.

**Step 3 — Verificare il parsing dei singoli eventi**
```
adb logcat | grep "\[SSE_PARSER\]"
```
- Se vedi `no 'payload' object found` con le chiavi reali → correggere l'unwrapping
- Se vedi `parsed as UnknownEvent` per eventi che dovrebbero essere `MessagePartUpdatedEvent` → il tipo string non corrisponde
- Se vedi `parsed as MessagePartUpdatedEvent` ma la UI è ancora vuota → il bug è nel ViewModel o nel threading

**Step 4 — Verificare i nomi dei campi JSON nei DTO**
Dal log `raw envelope`, controllare i nomi esatti dei campi in `properties.part`:
- `sessionID` vs `sessionId`
- `messageID` vs `messageId`
- `partID` vs `partId`
- `text` vs `content` vs altro

Se i nomi non corrispondono, aggiungere `[JsonPropertyName("...")]` ai DTO in `MessageDtos.cs`.

### File da modificare (in base alla diagnosi)

| File | Modifica probabile |
|------|-------------------|
| `src/openMob.Core/Infrastructure/Http/OpencodeApiClient.cs` | Correzione endpoint `/global/event` → `/event` (se Step 1 fallisce) |
| `src/openMob.Core/Helpers/ChatEventParser.cs` | Correzione unwrapping envelope (se Step 2 mostra struttura diversa) |
| `src/openMob.Core/Infrastructure/Http/Dtos/Opencode/MessageDtos.cs` | Aggiunta `[JsonPropertyName]` (se Step 4 mostra mismatch) |
| `src/openMob.Core/ViewModels/ChatViewModel.cs` | Correzione threading (se Step 3 mostra parsing ok ma UI vuota) |

### Contesto architetturale da rispettare

- Come stabilito in `spec-chat-service-layer` (2026-03-16), `ChatEventParser` non deve mai lanciare eccezioni — tutti i fallback devono restituire `UnknownEvent`.
- Come stabilito in `spec-chat-ui-completion` (2026-03-18), gli aggiornamenti UI devono passare per `IDispatcherService.RunOnMainThreadAsync`.
- Come stabilito in `spec-opencode-api-client` (2026-03-15), il client SSE usa il named client `"opencode-sse"` con timeout infinito.
- I log `#if DEBUG` introdotti in questa sessione devono essere preservati (REQ-009).
- La struttura wire di riferimento è documentata nel repo `anomalyco/opencode-sdk-js` (`src/resources/event.ts`): gli eventi hanno `type` e `properties` come campi di primo livello nell'oggetto evento, non annidati sotto `payload`.

### Ipotesi principale sul bug

Dal confronto tra il codice del `ChatEventParser` (che cerca `envelope.payload.type`) e la struttura TypeScript del SDK ufficiale (`event.ts`: `{ type: string, properties: {...} }`), il bug più probabile è che il server invii:
```json
{ "type": "message.part.updated", "properties": { "part": { ... } } }
```
mentre il parser cerca:
```json
{ "payload": { "type": "message.part.updated", "properties": { ... } } }
```
I log `[SSE_PARSER]` confermeranno o smentirono questa ipotesi prima di qualsiasi modifica al codice.

---

## Technical Analysis

> Added by: om-orchestrator | Date: 2026-03-19

### Change Classification

| Field | Value |
|-------|-------|
| Change type | Bug Fix |
| Git Flow branch | bugfix/fix-sse-real-time-streaming |
| Branches from | develop |
| Estimated complexity | Medium |
| Estimated agents involved | om-mobile-core, om-tester, om-reviewer |

### Layers Involved

| Layer | Agent | Scope |
|-------|-------|-------|
| Business logic / Parser | om-mobile-core | `src/openMob.Core/Helpers/ChatEventParser.cs` |
| ViewModels | om-mobile-core | `src/openMob.Core/ViewModels/ChatViewModel.cs` |
| Infrastructure / HTTP | om-mobile-core | `src/openMob.Core/Infrastructure/Http/OpencodeApiClient.cs` |
| Data / DTOs | om-mobile-core | `src/openMob.Core/Infrastructure/Http/Dtos/Opencode/MessageDtos.cs` |
| Unit Tests | om-tester | `tests/openMob.Tests/` |
| Code Review | om-reviewer | all of the above |

### Current State (as of 2026-03-19)

The branch `bugfix/fix-sse-real-time-streaming` already contains the core fix committed in `dd31bb4`:
- `ChatEventParser.Parse()` correctly unwraps the `{ "directory": ..., "payload": { "type": ..., "properties": ... } }` envelope
- `ChatViewModel` has `HandleMessagePartDelta` for real-time token streaming
- `MessageWithPartsDto.Parts` is nullable (server omits parts in some events)
- All SSE event handlers use `_dispatcher.Dispatch(...)` for thread safety
- `PartDto` has correct `[JsonPropertyName]` attributes (`sessionID`, `messageID`)

**Uncommitted working-tree changes** (must be committed):
- `ChatEventParser.cs` — `#if DEBUG` diagnostic logs re-added (REQ-009)
- `OpencodeApiClient.cs` — `#if DEBUG` diagnostic logs re-added (REQ-009)

**Open item**: The endpoint `/global/event` vs `/event` (REQ-002) cannot be verified without a live device. The `#if DEBUG` logs will surface this at runtime.

### Files to Modify

- `src/openMob.Core/Helpers/ChatEventParser.cs` — commit the `#if DEBUG` diagnostic logs (REQ-009)
- `src/openMob.Core/Infrastructure/Http/OpencodeApiClient.cs` — commit the `#if DEBUG` diagnostic logs (REQ-009)

### Files to Create (Tests)

- `tests/openMob.Tests/ViewModels/ChatViewModelSseTests.cs` — unit tests for SSE event handlers: `HandleMessagePartDelta`, `HandleMessagePartUpdated`, `HandleMessageUpdated` (REQ-003, REQ-004, REQ-005, REQ-006)

### Technical Dependencies

- `IDispatcherService` — already defined and injected in `ChatViewModel`
- `IChatService.SubscribeToEventsAsync` — already implemented
- `ChatEventParser` — already fixed in `dd31bb4`
- No new NuGet packages required

### Technical Risks

- **Endpoint `/global/event` vs `/event`**: Cannot be verified without a live device. The `#if DEBUG` logs will surface this at runtime. If `[SSE_RAW]` produces no output, the endpoint must be changed.
- **Pre-existing build warnings**: The solution has 7 pre-existing warnings (obsolete MAUI APIs in `SettingsPage.xaml.cs`, `ChatPage.xaml.cs`, `MauiThemeService.cs`). These are not introduced by this spec and are out of scope. AC-007 ("zero warnings") cannot be satisfied without addressing these pre-existing warnings — this is a known limitation.

### Execution Order

1. [om-mobile-core] Commit the uncommitted `#if DEBUG` diagnostic log changes in `ChatEventParser.cs` and `OpencodeApiClient.cs`
2. [om-tester] Write unit tests for `ChatViewModel` SSE event handlers (`HandleMessagePartDelta`, `HandleMessagePartUpdated`, `HandleMessageUpdated`) covering happy path, session ID mismatch, empty delta, and placeholder creation
3. [om-reviewer] Full review against spec
4. [Fix loop if needed] Address Critical and Major findings
5. [Git Flow] Finish bugfix branch and merge into develop

### Definition of Done

- [ ] REQ-001: `ChatEventParser` correctly parses the `payload`-wrapped envelope ✅ (already done in `dd31bb4`)
- [ ] REQ-002: Endpoint verified at runtime via `[SSE_RAW]` logs (cannot be automated)
- [ ] REQ-003: `HandleMessagePartUpdated` updates `TextContent` for `type: "text"` parts ✅ (already done)
- [ ] REQ-004: `HandleMessagePartDelta` appends delta text incrementally ✅ (already done)
- [ ] REQ-005: `HandleMessageUpdated` extracts text from parts ✅ (already done)
- [ ] REQ-006: All SSE handlers use `_dispatcher.Dispatch(...)` ✅ (already done)
- [ ] REQ-007: `PartDto` has correct `[JsonPropertyName]` attributes ✅ (already done)
- [ ] REQ-008: Unknown events return `UnknownEvent` without throwing ✅ (already done)
- [ ] REQ-009: `#if DEBUG` logs committed in both `ChatEventParser.cs` and `OpencodeApiClient.cs`
- [ ] Unit tests written for SSE event handlers in `ChatViewModel`
- [ ] `om-reviewer` verdict: ✅ Approved or ⚠️ Approved with remarks
- [ ] Git Flow bugfix branch finished and deleted
- [ ] Spec moved to `specs/done/` with Completed status
