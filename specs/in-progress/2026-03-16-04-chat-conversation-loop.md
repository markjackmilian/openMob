# Chat Conversation Loop — ChatViewModel, Invio Messaggi e SSE Streaming

## Metadata
| Field   | Value                        |
|---------|------------------------------|
| Date    | 2026-03-16                   |
| Status  | In Progress                  |
| Version | 1.0                          |

---

## Executive Summary

Implementa il loop conversazionale completo nel `ChatViewModel`: caricamento dello storico messaggi all'apertura di una sessione, invio di un prompt utente, ricezione in streaming della risposta AI tramite eventi SSE, gestione degli stati di caricamento/errore, e cancellazione della risposta in corso. Questa spec dipende da `IChatService` (Spec 02) e `IProjectPreferenceService` (Spec 03), e produce il contratto di binding che `ChatPage.xaml` (Spec 05) consumerà.

---

## Scope

### In Scope
- Refactoring di `ChatViewModel` per aggiungere l'intera superficie di binding per la chat
- `IQueryAttributable` per ricevere `sessionId` come parametro di navigazione da `FlyoutViewModel`
- `ObservableCollection<ChatMessage>` con logica di grouping (IsFirstInGroup / IsLastInGroup)
- `LoadMessagesCommand` — caricamento storico messaggi all'apertura sessione
- `SendMessageCommand` — invio prompt + ottimistic UI (aggiunge subito il messaggio utente)
- `CancelResponseCommand` — abort della risposta AI in corso
- Subscription SSE via `IChatService.SubscribeToEventsAsync` con lifecycle legato alla sessione attiva
- Gestione eventi SSE rilevanti: `MessageUpdatedEvent`, `MessagePartUpdatedEvent`, `SessionUpdatedEvent`, `SessionErrorEvent`
- Modello di dominio `ChatMessage` (domain model UI, non DTO)
- `SuggestionChip` model e collezione di chip predefiniti
- Stato `IsAiResponding`, `IsBusy`, `ErrorMessage`, `IsEmpty`
- Aggiornamento del titolo sessione quando arriva `SessionUpdatedEvent`
- Gestione errori: errori di rete, circuit breaker aperto, errori server
- Uso di `SelectedModelId` / `SelectedProviderId` (introdotti in Spec 03) nel `SendMessageCommand`

### Out of Scope
- XAML e UI (Spec 05)
- Unit test (Spec 06)
- Gestione permessi tool calls (spec futura)
- Visualizzazione rich content (code blocks, immagini, file diff) — spec futura
- Ricerca nei messaggi — spec futura
- Fork/revert sessione — spec futura
- Persistenza locale messaggi in SQLite — spec futura

---

## Functional Requirements

> Requirements are numbered for traceability.

### ChatMessage — Modello di Dominio

1. **[REQ-001]** Definire `ChatMessage` come sealed class (non record, perché le proprietà di grouping sono mutabili) in `src/openMob.Core/Models/ChatMessage.cs` con le seguenti proprietà:
   - `string Id` — da `MessageWithPartsDto.Info.Id`
   - `string SessionId` — da `MessageWithPartsDto.Info.SessionId`
   - `bool IsFromUser` — `true` se `Role == "user"`
   - `string TextContent` — testo estratto dalle parti di tipo `"text"` concatenate
   - `DateTimeOffset Timestamp` — estratto da `MessageWithPartsDto.Info.Time` (campo `created` in ms Unix)
   - `MessageDeliveryStatus DeliveryStatus` — enum: `Sending`, `Sent`, `Error`
   - `bool IsFirstInGroup` — calcolato dal ViewModel
   - `bool IsLastInGroup` — calcolato dal ViewModel
   - `bool IsStreaming` — `true` mentre la risposta AI è in arrivo (solo per messaggi assistant)
   - Constructor statico factory: `ChatMessage.FromDto(MessageWithPartsDto dto)`

2. **[REQ-002]** Definire `MessageDeliveryStatus` come enum in `src/openMob.Core/Models/MessageDeliveryStatus.cs`:
   - `Sending` — messaggio inviato ottimisticamente, in attesa di conferma server
   - `Sent` — confermato dal server
   - `Error` — invio fallito

3. **[REQ-003]** Definire `SuggestionChip` come sealed record in `src/openMob.Core/Models/SuggestionChip.cs`:
   - `string Title`
   - `string Subtitle`
   - `string PromptText` — testo da inserire nell'input bar quando il chip viene tappato

### ChatViewModel — Proprietà Osservabili

4. **[REQ-004]** `ChatViewModel` deve esporre le seguenti proprietà `[ObservableProperty]`:
   - `ObservableCollection<ChatMessage> Messages` — inizializzata vuota
   - `string InputText` — testo corrente nell'input bar, default `string.Empty`
   - `bool IsBusy` — `true` durante `LoadMessagesCommand`
   - `bool IsAiResponding` — `true` mentre la risposta AI è in streaming
   - `bool IsEmpty` — `true` quando `Messages.Count == 0` (calcolato, `[NotifyPropertyChangedFor]`)
   - `string? ErrorMessage` — messaggio di errore da mostrare, `null` se nessun errore
   - `bool HasError` — `true` quando `ErrorMessage != null`
   - `ObservableCollection<SuggestionChip> SuggestionChips` — chip predefiniti
   - `string SessionTitle` — titolo della sessione corrente
   - `string? SessionId` — ID sessione corrente, `null` se nessuna sessione attiva

5. **[REQ-005]** `ChatViewModel` deve implementare `IQueryAttributable` per ricevere il parametro di navigazione `sessionId`:
   ```csharp
   public void ApplyQueryAttributes(IDictionary<string, object> query)
   ```
   Quando `sessionId` cambia, cancellare la subscription SSE precedente, aggiornare `SessionId`, e invocare `LoadMessagesCommand`.

### ChatViewModel — Comandi

6. **[REQ-006]** `LoadMessagesCommand` (`[AsyncRelayCommand]`):
   - Imposta `IsBusy = true`, `ErrorMessage = null`
   - Chiama `IChatService.GetMessagesAsync(SessionId)`
   - In caso di successo: popola `Messages` con i `ChatMessage` mappati dai DTO, calcola il grouping, imposta `IsBusy = false`
   - In caso di errore: imposta `ErrorMessage` con messaggio localizzato, `IsBusy = false`
   - Avvia la subscription SSE per la sessione corrente (se non già attiva)

7. **[REQ-007]** `SendMessageCommand` (`[AsyncRelayCommand]`, `CanExecute`: `InputText` non vuoto e non `IsAiResponding`):
   - Cattura il testo da `InputText` e lo svuota immediatamente
   - Aggiunge ottimisticamente un `ChatMessage` con `IsFromUser = true`, `DeliveryStatus = Sending` alla collezione `Messages`
   - Ricalcola il grouping
   - Chiama `IChatService.SendPromptAsync(SessionId, text, selectedModelId, selectedProviderId)`
   - `selectedModelId` e `selectedProviderId` sono estratti da `SelectedModelId` (introdotto in Spec 03) via `Split('/', 2)`; se `SelectedModelId` è null, passare `null` a entrambi
   - In caso di successo: aggiorna `DeliveryStatus = Sent` sul messaggio ottimistico
   - In caso di errore: aggiorna `DeliveryStatus = Error`, imposta `ErrorMessage`
   - Imposta `IsAiResponding = true` (la risposta arriverà via SSE)

8. **[REQ-008]** `CancelResponseCommand` (`[RelayCommand]`, `CanExecute`: `IsAiResponding == true`):
   - Chiama `IOpencodeApiClient.AbortSessionAsync(SessionId)` (o `IChatService` se esposto)
   - Imposta `IsAiResponding = false`
   - Non rimuove i messaggi parziali già ricevuti via SSE

9. **[REQ-009]** `SelectSuggestionChipCommand` (`[RelayCommand]`, parametro: `SuggestionChip`):
   - Imposta `InputText = chip.PromptText`
   - Invoca `SendMessageCommand`

10. **[REQ-010]** `DismissErrorCommand` (`[RelayCommand]`):
    - Imposta `ErrorMessage = null`

### ChatViewModel — Gestione SSE

11. **[REQ-011]** `ChatViewModel` deve avviare la subscription SSE tramite `IChatService.SubscribeToEventsAsync` quando una sessione viene caricata. La subscription deve girare su un `Task` in background (non bloccare il thread UI). Usare un `CancellationTokenSource` interno per controllarne il lifecycle.

12. **[REQ-012]** Gestione `MessageUpdatedEvent`:
    - Se il messaggio è già in `Messages` (stesso `Id`): aggiornare `TextContent`, `IsStreaming`, `DeliveryStatus`
    - Se il messaggio non è in `Messages`: aggiungerlo come nuovo `ChatMessage`
    - Ricalcolare il grouping dopo ogni modifica
    - Se `IsFromUser == false` e il messaggio è completo (non streaming): impostare `IsAiResponding = false`

13. **[REQ-013]** Gestione `MessagePartUpdatedEvent`:
    - Trovare il `ChatMessage` corrispondente per `MessageId`
    - Se la parte è di tipo `"text"`: aggiornare `TextContent` con il testo aggiornato
    - Mantenere `IsStreaming = true` finché non arriva `MessageUpdatedEvent` con messaggio completo

14. **[REQ-014]** Gestione `SessionUpdatedEvent`:
    - Aggiornare `SessionTitle` con il titolo aggiornato dalla sessione

15. **[REQ-015]** Gestione `SessionErrorEvent`:
    - Impostare `IsAiResponding = false`
    - Impostare `ErrorMessage` con il messaggio di errore ricevuto
    - Aggiornare `DeliveryStatus = Error` sull'ultimo messaggio utente in `Messages` se presente

### ChatViewModel — Grouping Messaggi

16. **[REQ-016]** Il metodo privato `RecalculateGrouping()` deve iterare `Messages` e impostare `IsFirstInGroup` / `IsLastInGroup` su ogni `ChatMessage`:
    - Un messaggio è `IsFirstInGroup = true` se è il primo della collezione oppure se il messaggio precedente ha `IsFromUser` diverso
    - Un messaggio è `IsLastInGroup = true` se è l'ultimo della collezione oppure se il messaggio successivo ha `IsFromUser` diverso
    - `RecalculateGrouping()` deve essere chiamato dopo ogni modifica alla collezione `Messages`

### ChatViewModel — Suggestion Chips

17. **[REQ-017]** `SuggestionChips` deve essere popolata nel costruttore con 4 chip predefiniti (hardcoded per questa spec):
    - `{ Title: "Spiega questo codice", Subtitle: "Analisi dettagliata", PromptText: "Spiega questo codice in dettaglio" }`
    - `{ Title: "Trova i bug", Subtitle: "Revisione critica", PromptText: "Trova eventuali bug o problemi in questo codice" }`
    - `{ Title: "Scrivi i test", Subtitle: "Unit test completi", PromptText: "Scrivi unit test completi per questo codice" }`
    - `{ Title: "Refactoring", Subtitle: "Migliora la struttura", PromptText: "Suggerisci un refactoring per migliorare questo codice" }`

### ChatViewModel — Lifecycle

18. **[REQ-018]** `ChatViewModel` deve implementare `IDisposable`. Nel metodo `Dispose()`:
    - Cancellare e disporre il `CancellationTokenSource` della subscription SSE
    - Svuotare `Messages` e `SuggestionChips`

---

## Functional Impacts

### Affected Components / Systems
| Component | Impact | Notes |
|-----------|--------|-------|
| `src/openMob.Core/ViewModels/ChatViewModel.cs` | Modifica maggiore | Aggiunta intera superficie di binding per il loop conversazionale |
| `src/openMob.Core/Models/ChatMessage.cs` | Nuovo file | Domain model UI |
| `src/openMob.Core/Models/MessageDeliveryStatus.cs` | Nuovo file | Enum stato consegna |
| `src/openMob.Core/Models/SuggestionChip.cs` | Nuovo file | Model chip suggerimento |
| `src/openMob.Core/Infrastructure/DI/CoreServiceExtensions.cs` | Modifica | Registrazione ChatViewModel aggiornata |
| `src/openMob/Views/Pages/ChatPage.xaml` | Dipendente | Consumerà le proprietà definite qui (Spec 05) |

### Dependencies
- **Spec 02 (`chat-service-layer`)** — `IChatService`, `ChatEvent`, `ChatServiceResult` devono essere implementati prima di questa spec
- **Spec 03 (`dynamic-provider-model-selection`)** — `IProjectPreferenceService` e le proprietà `SelectedModelId`/`SelectedModelName` su `ChatViewModel` devono essere disponibili prima di questa spec
- `IOpencodeApiClient.AbortSessionAsync` — già disponibile per `CancelResponseCommand`
- `FlyoutViewModel.SelectSessionAsync` — già naviga a `//chat?sessionId=<id>`, `ChatViewModel` deve ricevere il parametro via `IQueryAttributable`
- `CommunityToolkit.Mvvm` — `[ObservableProperty]`, `[RelayCommand]`, `[AsyncRelayCommand]`, `ObservableObject`

---

## Open Questions & Clarifications

| # | Question | Status | Answer / Decision |
|---|----------|--------|-------------------|
| 1 | `selectedModelId` e `selectedProviderId` in `SendMessageCommand` — da dove vengono? | Resolved | Da `SelectedModelId` introdotto in Spec 03. Splittare con `Split('/', 2)` per ottenere provider e model. Se null, passare null al servizio (il server usa il default). |
| 2 | Il grouping deve considerare anche il tempo tra messaggi (es. gap > 5 min = nuovo gruppo)? | Resolved | No per questa spec. Il grouping è basato solo sul cambio di `IsFromUser`. Aggiungere grouping temporale in una spec futura. |
| 3 | Quando `SessionId` cambia (utente seleziona altra sessione dal flyout), i messaggi precedenti devono essere svuotati immediatamente o dopo il caricamento dei nuovi? | Resolved | Svuotare immediatamente prima di `LoadMessagesCommand` per evitare flash di contenuto vecchio. |
| 4 | `IsAiResponding` deve tornare `false` anche se arriva solo `MessageUpdatedEvent` senza `MessagePartUpdatedEvent` precedenti? | Resolved | Sì. Qualsiasi `MessageUpdatedEvent` con `role == "assistant"` e messaggio non-streaming imposta `IsAiResponding = false`. |
| 5 | Come determinare se un messaggio assistant è "completo" (non più in streaming) dall'evento SSE? | Resolved | In `ChatMessage.FromDto()`, check if `MessageInfoDto.Time` raw JSON contains a `completed` property that is non-null. If present, the message is complete (`IsStreaming = false`). If absent or null, the message is still streaming (`IsStreaming = true`, assistant messages only). User messages are always `IsStreaming = false`. This does not require changes to `ChatEventParser` — the info is already in `MessageWithPartsDto.Info.Time`. |

---

## Acceptance Criteria

> Each criterion maps to one or more functional requirements.

- [ ] **[AC-001]** Dato un `sessionId` passato come query parameter di navigazione, quando `ChatViewModel.ApplyQueryAttributes` viene chiamato, allora `SessionId` viene aggiornato e `LoadMessagesCommand` viene invocato automaticamente. *(REQ-005)*
- [ ] **[AC-002]** Dato `LoadMessagesCommand` eseguito con successo, quando il servizio restituisce messaggi, allora `Messages` è popolata con `ChatMessage` mappati correttamente e `IsBusy = false`. *(REQ-006)*
- [ ] **[AC-003]** Dato `Messages` vuota, quando si verifica la proprietà `IsEmpty`, allora è `true`; quando viene aggiunto un messaggio, `IsEmpty` diventa `false`. *(REQ-004)*
- [ ] **[AC-004]** Dato un testo nell'input bar, quando `SendMessageCommand` viene eseguito, allora `InputText` viene svuotato, un `ChatMessage` ottimistico con `DeliveryStatus = Sending` viene aggiunto a `Messages`, e `IChatService.SendPromptAsync` viene chiamato. *(REQ-007)*
- [ ] **[AC-005]** Dato un errore di rete durante `SendPromptAsync`, quando il comando fallisce, allora `DeliveryStatus = Error` sul messaggio ottimistico e `ErrorMessage` è non-null. *(REQ-007)*
- [ ] **[AC-006]** Dato `IsAiResponding = true`, quando `CancelResponseCommand` viene eseguito, allora `AbortSessionAsync` viene chiamato e `IsAiResponding` diventa `false`. *(REQ-008)*
- [ ] **[AC-007]** Dato un `MessageUpdatedEvent` SSE per un messaggio non ancora in `Messages`, quando l'evento viene processato, allora il messaggio viene aggiunto alla collezione e il grouping viene ricalcolato. *(REQ-012)*
- [ ] **[AC-008]** Dato un `SessionErrorEvent` SSE, quando l'evento viene processato, allora `IsAiResponding = false` e `ErrorMessage` contiene il messaggio di errore. *(REQ-015)*
- [ ] **[AC-009]** Dati due messaggi consecutivi dello stesso mittente, quando il grouping viene calcolato, allora il primo ha `IsFirstInGroup = true` e `IsLastInGroup = false`, il secondo ha `IsFirstInGroup = false` e `IsLastInGroup = true`. *(REQ-016)*
- [ ] **[AC-010]** Dato un chip selezionato, quando `SelectSuggestionChipCommand` viene eseguito, allora `InputText` viene impostato con `chip.PromptText` e `SendMessageCommand` viene invocato. *(REQ-009)*
- [ ] **[AC-011]** Dato `ChatViewModel` disposto, quando `Dispose()` viene chiamato, allora la subscription SSE viene cancellata e `Messages` viene svuotata. *(REQ-018)*

---

## Notes for Technical Analysis

> This section is addressed to the agent that will perform the technical implementation analysis.

- **`IQueryAttributable`**: interfaccia di `Microsoft.Maui.Controls` — attenzione, è nel namespace MAUI. Verificare se può essere implementata in `openMob.Core` (che deve avere zero dipendenze MAUI). Alternativa: definire un'interfaccia custom `ISessionNavigatable` in Core con metodo `SetSession(string sessionId)`, e fare in modo che `ChatPage.xaml.cs` chiami `ViewModel.SetSession(sessionId)` nel `OnNavigatedTo` override. Questa è la soluzione preferita per rispettare la layer separation.
- **SSE subscription lifecycle**: usare `CancellationTokenSource` con `CreateLinkedTokenSource` per combinare il token della sessione con quello del ViewModel. Quando la sessione cambia, cancellare il token precedente e crearne uno nuovo.
- **Thread safety per `Messages`**: `ObservableCollection` non è thread-safe. Tutti gli aggiornamenti da eventi SSE (che arrivano su thread background) devono essere marshallati sul thread UI via `MainThread.BeginInvokeOnMainThread` o `IDispatcher`. Usare `IDispatcher` (iniettato) per testabilità.
- **Ottimistic UI**: il messaggio utente viene aggiunto prima della conferma server. Se il server restituisce errore, il messaggio rimane in `Messages` con `DeliveryStatus = Error` (non viene rimosso) per permettere all'utente di vedere cosa ha scritto.
- **`ChatMessage` come class, non record**: le proprietà `IsFirstInGroup`, `IsLastInGroup`, `IsStreaming`, `DeliveryStatus` vengono mutate dopo la creazione. Implementare `INotifyPropertyChanged` su `ChatMessage` (o ereditare da `ObservableObject`) per permettere binding reattivo nella UI.
- **`TextContent` da `Parts`**: estrarre il testo concatenando tutte le parti con `Type == "text"` e leggendo il campo `text` dal `Payload` JSON. Gestire il caso in cui `Payload` non contenga `text` (restituire stringa vuota).
- **As established in `specs/in-progress/2026-03-14-chat-ui-design-guidelines.md`**: `ChatViewModel` deve esporre `ObservableCollection<SuggestionChip>` e la logica di grouping (`IsFirstInGroup`/`IsLastInGroup`) come proprietà del modello, non come converter XAML.
- **`IDispatcher` injection**: iniettare `IDispatcher` nel costruttore di `ChatViewModel` per marshalling thread-safe. In produzione viene risolto da MAUI DI; nei test viene mockato con NSubstitute.
- **Integrazione con Spec 03**: `SelectedModelId` e `SelectedModelName` sono già presenti su `ChatViewModel` dopo Spec 03. Questa spec aggiunge solo l'uso di `SelectedModelId` in `SendMessageCommand` per estrarre `providerId` e `modelId`.

---

## Technical Analysis

> Added by: om-orchestrator | Date: 2026-03-18

### Change Classification

| Field | Value |
|-------|-------|
| Change type | Feature |
| Git Flow branch | `feature/chat-conversation-loop` |
| Branches from | `develop` |
| Estimated complexity | High |
| Estimated agents involved | om-mobile-core, om-tester, om-reviewer |

### Layers Involved

| Layer | Agent | Scope |
|-------|-------|-------|
| Domain Models | om-mobile-core | `src/openMob.Core/Models/` |
| ViewModels | om-mobile-core | `src/openMob.Core/ViewModels/` |
| DI Registration | om-mobile-core | `src/openMob.Core/Infrastructure/DI/` |
| Dispatcher Abstraction | om-mobile-core | `src/openMob.Core/Services/` |
| Unit Tests | om-tester | `tests/openMob.Tests/` |
| Code Review | om-reviewer | all of the above |

> **No om-mobile-ui involvement** — this spec explicitly excludes XAML/UI (deferred to Spec 05).

### Files to Create

- `src/openMob.Core/Models/ChatMessage.cs` — Sealed class inheriting `ObservableObject`. Domain model for UI-bound chat messages with mutable grouping/streaming/delivery properties. Static factory `FromDto(MessageWithPartsDto)` maps from DTO. [REQ-001]
- `src/openMob.Core/Models/MessageDeliveryStatus.cs` — Enum: `Sending`, `Sent`, `Error`. [REQ-002]
- `src/openMob.Core/Models/SuggestionChip.cs` — Sealed record with `Title`, `Subtitle`, `PromptText`. [REQ-003]
- `src/openMob.Core/Services/IDispatcherService.cs` — Abstraction over UI thread dispatching for testability. Single method: `void Dispatch(Action action)`. The MAUI project provides the concrete implementation wrapping `MainThread.BeginInvokeOnMainThread`. [Technical decision — see Notes]

### Files to Modify

- `src/openMob.Core/ViewModels/ChatViewModel.cs` — Major refactoring: add `IDisposable`, add all observable properties (Messages, InputText, IsBusy, IsAiResponding, IsEmpty, ErrorMessage, HasError, SuggestionChips, SessionTitle), add commands (LoadMessages, SendMessage, CancelResponse, SelectSuggestionChip, DismissError), add SSE subscription lifecycle, add grouping logic, add `SetSession(string)` method. Constructor gains `IChatService`, `IOpencodeApiClient`, `IDispatcherService` dependencies. [REQ-004 through REQ-018]
- `src/openMob.Core/Infrastructure/DI/CoreServiceExtensions.cs` — Register `IDispatcherService` is NOT done here (MAUI project registers the concrete implementation). No change needed for `ChatViewModel` registration (already `AddTransient`).

### Technical Decisions

#### 1. IQueryAttributable replacement → `SetSession(string sessionId)` method

`IQueryAttributable` lives in `Microsoft.Maui.Controls` — implementing it in `openMob.Core` would break the zero-MAUI-dependency rule. Instead:

- `ChatViewModel` exposes a public `SetSession(string sessionId)` method
- `ChatPage.xaml.cs` implements `IQueryAttributable` and calls `ViewModel.SetSession(sessionId)` in its override
- This respects layer separation and keeps the ViewModel fully testable without MAUI references

**Impact on REQ-005**: The spec's `ApplyQueryAttributes` signature is replaced by `SetSession(string)`. The behavioral contract (cancel previous SSE, update SessionId, invoke LoadMessages) remains identical.

#### 2. IDispatcherService abstraction

The codebase currently has no dispatcher abstraction. SSE events arrive on background threads and must marshal to the UI thread for `ObservableCollection` updates. Creating `IDispatcherService` with a `Dispatch(Action)` method allows:
- NSubstitute mocking in tests (mock executes the action synchronously)
- MAUI project provides `MainThreadDispatcherService` wrapping `MainThread.BeginInvokeOnMainThread`

#### 3. ChatMessage inherits ObservableObject

`ChatMessage` has 4 mutable properties (`IsFirstInGroup`, `IsLastInGroup`, `IsStreaming`, `DeliveryStatus`, `TextContent`) that the UI needs to observe reactively. Inheriting from `ObservableObject` and using `[ObservableProperty]` is the cleanest approach consistent with the project's patterns.

#### 4. Open Question #5 resolution — streaming detection

`MessageInfoDto.Time` is a `JsonElement`. For assistant messages, the shape is `{ created: number, completed?: number }`. In `ChatMessage.FromDto()`:
- If `time` has a `completed` property with `ValueKind != Null` → `IsStreaming = false`
- If `completed` is absent or null → `IsStreaming = true` (assistant only; user messages always `false`)

#### 5. SessionId property naming

The existing `ChatViewModel` has `CurrentSessionId`. The spec calls it `SessionId`. To avoid a breaking rename (the property is likely used by `ChatPage.xaml` and `FlyoutViewModel`), **keep `CurrentSessionId` as the canonical name** and treat spec's `SessionId` as an alias. The spec's `SessionTitle` maps to the existing `SessionName`.

### Technical Dependencies

- `IChatService` (Spec 02) — `GetMessagesAsync`, `SendPromptAsync`, `SubscribeToEventsAsync` — all available and merged
- `IOpencodeApiClient.AbortSessionAsync` — available for `CancelResponseCommand`
- `SelectedModelId` / `SelectedModelName` on `ChatViewModel` (Spec 03) — already present
- `ModelIdHelper.ExtractModelName` — already available in `src/openMob.Core/Helpers/`
- `SendPromptRequestBuilder.FromText` — already available (not directly used by ViewModel, but by ChatService)
- `MessageWithPartsDto`, `PartDto`, `MessageInfoDto` — already available in DTOs
- `ChatEvent` hierarchy (`MessageUpdatedEvent`, `MessagePartUpdatedEvent`, `SessionUpdatedEvent`, `SessionErrorEvent`) — already available
- `ChatServiceResult<T>` — already available
- No new NuGet packages required
- No EF Core migrations required

### Technical Risks

| Risk | Mitigation |
|------|------------|
| Thread safety: `ObservableCollection` updated from SSE background thread | `IDispatcherService.Dispatch()` marshals all collection mutations to UI thread. Tests mock it with synchronous execution. |
| Property naming conflict: spec says `SessionId`/`SessionTitle` but existing code has `CurrentSessionId`/`SessionName` | Keep existing property names to avoid breaking existing XAML bindings. Document the mapping. |
| SSE subscription leak: if `SetSession` is called rapidly (user taps multiple sessions quickly) | Use `CancellationTokenSource` per session. `SetSession` cancels previous CTS before creating new one. |
| `ChatMessage.FromDto` parsing: `Time` JSON may not contain `completed` field | Defensive check with `TryGetProperty("completed", ...)` — gracefully defaults to `IsStreaming = true` for assistant messages. |
| Large message history: loading 100+ messages into `ObservableCollection` | Single batch `Clear()` + `foreach Add()` — acceptable for v1. Future spec can add virtualization/paging. |

### Execution Order

> Steps that can run in parallel are marked with ⟳. Steps that must be sequential are numbered.

1. **[Git Flow]** Create branch `feature/chat-conversation-loop` from `develop`
2. **[om-mobile-core]** Implement all domain models (`ChatMessage`, `MessageDeliveryStatus`, `SuggestionChip`), `IDispatcherService` interface, and refactor `ChatViewModel` with full binding surface, commands, SSE handling, and lifecycle management
3. **[om-tester]** Write unit tests for `ChatMessage.FromDto`, `ChatViewModel` (all commands, SSE event handling, grouping logic, lifecycle)
4. **[om-reviewer]** Full review against spec — all REQ and AC
5. **[Fix loop if needed]** Address Critical and Major findings
6. **[Git Flow]** Finish branch and merge into `develop`

### Definition of Done

- [ ] All `[REQ-001]` through `[REQ-018]` requirements implemented
- [ ] All `[AC-001]` through `[AC-011]` acceptance criteria satisfied
- [ ] Unit tests written for `ChatMessage`, `ChatViewModel` (all commands, SSE event handlers, grouping, lifecycle)
- [ ] `om-reviewer` verdict: Approved or Approved with remarks
- [ ] Git Flow branch finished and deleted
- [ ] Spec moved to `specs/done/` with Completed status
