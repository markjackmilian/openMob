# Chat Conversation Loop — ChatViewModel, Invio Messaggi e SSE Streaming

## Metadata
| Field   | Value                        |
|---------|------------------------------|
| Date    | 2026-03-16                   |
| Status  | Draft                        |
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
| 5 | Come determinare se un messaggio assistant è "completo" (non più in streaming) dall'evento SSE? | Open | Da verificare sul wire format opencode. Probabilmente il campo `time.completed` è presente nel DTO quando il messaggio è completo. `ChatEventParser` deve esporre questa informazione. |

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
