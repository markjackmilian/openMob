# Chat Service Layer — IChatService, Resilienza HTTP e SSE Reconnect

## Metadata
| Field       | Value                                    |
|-------------|------------------------------------------|
| Date        | 2026-03-16                               |
| Status      | **Completed**                            |
| Version     | 1.0                                      |
| Completed   | 2026-03-17                               |
| Branch      | feature/chat-service-layer (merged)      |
| Merged into | develop                                  |

---

## Executive Summary

Introduce `IChatService` e la sua implementazione `ChatService` come strato di servizio dedicato alle operazioni di messaggistica (invio prompt, caricamento storico messaggi) che si interpone tra i ViewModel e `IOpencodeApiClient`. Aggiunge resilienza HTTP tramite `Microsoft.Extensions.Http.Resilience` (retry con backoff esponenziale, circuit breaker, timeout) e gestisce il reconnect automatico della subscription SSE con supporto `Last-Event-ID` per riprendere lo stream dal punto di interruzione. Definisce inoltre il modello di dominio completo degli eventi SSE (`ChatEventType` enum + typed event wrappers) che sarà consumato da `ChatViewModel` nella spec successiva.

---

## Scope

### In Scope
- Interfaccia `IChatService` con metodi per invio prompt e caricamento messaggi
- Implementazione `ChatService` che delega a `IOpencodeApiClient`
- Aggiunta del package `Microsoft.Extensions.Http.Resilience` e configurazione della pipeline di resilienza per il named HttpClient `"opencode"`
- Retry policy: 3 tentativi, backoff esponenziale (1s, 2s, 4s), solo su errori transitori (5xx, timeout, network)
- Circuit breaker: apre dopo 5 fallimenti consecutivi in 30s, half-open dopo 15s
- Timeout per singola request: 30s
- SSE reconnect automatico con `Last-Event-ID` header per riprendere lo stream dal punto di interruzione
- Modello di dominio eventi SSE: enum `ChatEventType` + sealed record typed per ogni tipo di evento
- Registrazione DI di `IChatService` / `ChatService` in `CoreServiceExtensions`
- Helper `SendPromptRequestBuilder` per costruire `SendPromptRequest` da testo plain (risolve il gap del wire format `Parts`)

### Out of Scope
- Caching locale dei messaggi in SQLite (spec futura)
- Logica di business nel ViewModel (Spec 04)
- UI e XAML (Spec 05)
- Unit test (Spec 06)
- Gestione permessi tool calls (spec futura dedicata)
- Invio di parti non-testo (immagini, file) — future spec

---

## Functional Requirements

> Requirements are numbered for traceability.

### IChatService — Interfaccia

1. **[REQ-001]** `IChatService` deve esporre il metodo:
   ```
   Task<ChatServiceResult<MessageWithPartsDto>> SendPromptAsync(
       string sessionId,
       string text,
       string? modelId,
       string? providerId,
       CancellationToken ct = default)
   ```
   Costruisce internamente il `SendPromptRequest` con la parte testo e delega a `IOpencodeApiClient.SendPromptAsyncNoWait`. Restituisce un `ChatServiceResult<T>` che incapsula successo/errore senza lanciare eccezioni.

2. **[REQ-002]** `IChatService` deve esporre il metodo:
   ```
   Task<ChatServiceResult<IReadOnlyList<MessageWithPartsDto>>> GetMessagesAsync(
       string sessionId,
       int? limit = null,
       CancellationToken ct = default)
   ```
   Delega a `IOpencodeApiClient.GetMessagesAsync`.

3. **[REQ-003]** `IChatService` deve esporre il metodo:
   ```
   IAsyncEnumerable<ChatEvent> SubscribeToEventsAsync(CancellationToken ct = default)
   ```
   Gestisce la subscription SSE con reconnect automatico. Yield di `ChatEvent` typed (non raw `OpencodeEventDto`). Mantiene internamente l'ultimo `EventId` ricevuto per il reconnect.

4. **[REQ-004]** `IChatService` deve esporre la proprietà:
   ```
   bool IsConnected { get; }
   event Action<bool>? IsConnectedChanged;
   ```
   Riflette lo stato della connessione SSE. `true` dopo il primo evento `server.connected`, `false` quando il stream si interrompe o il circuit breaker è aperto.

### ChatServiceResult

5. **[REQ-005]** Definire `ChatServiceResult<T>` come sealed record con:
   - `bool IsSuccess`
   - `T? Value` (non-null se `IsSuccess`)
   - `ChatServiceError? Error` (non-null se `!IsSuccess`)
   - Factory methods: `ChatServiceResult<T>.Ok(T value)` e `ChatServiceResult<T>.Fail(ChatServiceError error)`

6. **[REQ-006]** Definire `ChatServiceError` come sealed record con:
   - `ChatServiceErrorKind Kind` (enum: `NetworkError`, `ServerError`, `Timeout`, `CircuitOpen`, `Cancelled`, `Unknown`)
   - `string Message`
   - `int? HttpStatusCode` (nullable, presente per errori HTTP)

### Modello Domini Eventi SSE

7. **[REQ-007]** Definire l'enum `ChatEventType` con i seguenti valori, mappati ai tipi di evento opencode:

   | Valore enum | Stringa evento opencode | Descrizione |
   |---|---|---|
   | `ServerConnected` | `server.connected` | Connessione SSE stabilita |
   | `MessageUpdated` | `message.updated` | Messaggio aggiornato (streaming testo o completamento) |
   | `MessagePartUpdated` | `message.part.updated` | Singola parte del messaggio aggiornata |
   | `SessionUpdated` | `session.updated` | Metadati sessione aggiornati |
   | `SessionError` | `session.error` | Errore durante l'elaborazione della sessione |
   | `PermissionRequested` | `permission.requested` | L'AI richiede un permesso (tool call) |
   | `PermissionUpdated` | `permission.updated` | Stato permesso aggiornato |
   | `Unknown` | qualsiasi altro valore | Tipo non riconosciuto, payload raw preservato |

8. **[REQ-008]** Definire la classe base astratta `ChatEvent` con proprietà:
   - `ChatEventType Type`
   - `string? RawEventId`

9. **[REQ-009]** Definire i seguenti sealed record che ereditano da `ChatEvent`:

   | Tipo | Proprietà aggiuntive |
   |---|---|
   | `ServerConnectedEvent` | nessuna |
   | `MessageUpdatedEvent` | `MessageWithPartsDto Message` |
   | `MessagePartUpdatedEvent` | `PartDto Part` |
   | `SessionUpdatedEvent` | `SessionDto Session` |
   | `SessionErrorEvent` | `string SessionId`, `string ErrorMessage` |
   | `PermissionRequestedEvent` | `string SessionId`, `string PermissionId`, `JsonElement RawPayload` |
   | `PermissionUpdatedEvent` | `string SessionId`, `string PermissionId`, `JsonElement RawPayload` |
   | `UnknownEvent` | `string RawType`, `JsonElement? RawData` |

10. **[REQ-010]** Definire `ChatEventParser` (sealed class, internal) responsabile di deserializzare `OpencodeEventDto` → `ChatEvent`. Ogni tipo noto viene deserializzato nel typed record corrispondente. In caso di errore di deserializzazione, restituisce `UnknownEvent` con i dati raw preservati (mai lancia eccezione).

### SSE Reconnect

11. **[REQ-011]** `ChatService.SubscribeToEventsAsync` deve implementare il reconnect automatico:
    - Quando lo stream SSE si interrompe (eccezione o fine stream inattesa), attendere un backoff (1s al primo tentativo, raddoppiando fino a max 30s) e riaprire la connessione.
    - Includere l'header `Last-Event-ID: <lastEventId>` nella nuova request se un `EventId` è stato ricevuto in precedenza.
    - Il reconnect si interrompe solo quando il `CancellationToken` viene cancellato.
    - Aggiornare `IsConnected = false` durante il backoff, `IsConnected = true` al ricevimento del primo evento dopo il reconnect.

12. **[REQ-012]** Il numero massimo di tentativi di reconnect consecutivi senza ricevere alcun evento è **10**. Superato questo limite, `SubscribeToEventsAsync` completa il `IAsyncEnumerable` senza ulteriori tentativi e imposta `IsConnected = false`.

### Resilienza HTTP

13. **[REQ-013]** Aggiungere il package NuGet `Microsoft.Extensions.Http.Resilience` al progetto `openMob.Core`.

14. **[REQ-014]** Configurare la pipeline di resilienza sul named HttpClient `"opencode"` in `CoreServiceExtensions.AddOpenMobCore()`:
    - **Timeout per request**: 30 secondi
    - **Retry**: 3 tentativi, backoff esponenziale con jitter (base 1s), solo su `HttpRequestException`, `TaskCanceledException` (timeout), e status code 5xx
    - **Circuit breaker**: si apre dopo 5 fallimenti in una finestra di 30s; rimane aperto per 15s; poi passa a half-open (1 request di test)
    - La pipeline NON si applica alle request SSE (`SubscribeToEventsAsync`) che gestiscono la propria logica di reconnect.

15. **[REQ-015]** `ChatService` deve propagare correttamente lo stato del circuit breaker: quando il circuit è aperto e viene chiamato `SendPromptAsync` o `GetMessagesAsync`, restituire `ChatServiceResult.Fail` con `Kind = CircuitOpen` senza effettuare la chiamata HTTP.

### SendPromptRequestBuilder

16. **[REQ-016]** Definire `SendPromptRequestBuilder` (sealed class) con metodo statico:
    ```
    static SendPromptRequest FromText(string text, string? modelId = null, string? providerId = null)
    ```
    Costruisce un `SendPromptRequest` con `Parts` contenente un singolo elemento JSON corrispondente al wire format opencode per una parte testo: `{ "type": "text", "text": "<testo>" }`.

### Registrazione DI

17. **[REQ-017]** `CoreServiceExtensions.AddOpenMobCore()` deve registrare:
    - `IChatService` → `ChatService` (Singleton, stessa lifetime di `IOpencodeApiClient`)

---

## Functional Impacts

### Affected Components / Systems
| Component | Impact | Notes |
|-----------|--------|-------|
| `src/openMob.Core/Infrastructure/DI/CoreServiceExtensions.cs` | Modifica | Aggiunta resilienza HttpClient + registrazione IChatService |
| `src/openMob.Core/Infrastructure/Http/OpencodeApiClient.cs` | Nessuna modifica | Beneficia automaticamente della pipeline di resilienza sul named client |
| `src/openMob.Core/Services/IChatService.cs` | Nuovo file | Interfaccia pubblica |
| `src/openMob.Core/Services/ChatService.cs` | Nuovo file | Implementazione |
| `src/openMob.Core/Models/ChatEvent.cs` | Nuovo file | Gerarchia eventi SSE typed |
| `src/openMob.Core/Models/ChatEventType.cs` | Nuovo file | Enum tipi evento |
| `src/openMob.Core/Models/ChatServiceResult.cs` | Nuovo file | Result type |
| `src/openMob.Core/Models/ChatServiceError.cs` | Nuovo file | Error type |
| `src/openMob.Core/Helpers/ChatEventParser.cs` | Nuovo file | Parser OpencodeEventDto → ChatEvent |
| `src/openMob.Core/Helpers/SendPromptRequestBuilder.cs` | Nuovo file | Builder per SendPromptRequest |

### Dependencies
- `IOpencodeApiClient` (già implementato e testato) — `ChatService` lo consuma via constructor injection
- `Microsoft.Extensions.Http.Resilience` — nuovo package NuGet da aggiungere a `openMob.Core.csproj`
- `MessageWithPartsDto`, `PartDto`, `SessionDto`, `OpencodeEventDto` — DTOs già definiti, nessuna modifica
- `SendPromptRequest` — già definito, `SendPromptRequestBuilder` lo costruisce

---

## Open Questions & Clarifications

| # | Question | Status | Answer / Decision |
|---|----------|--------|-------------------|
| 1 | `SendPromptAsyncNoWait` vs `SendPromptAsync` — quale usare in `IChatService.SendPromptAsync`? | Resolved | Usare `SendPromptAsyncNoWait` (fire-and-forget, HTTP 204). La risposta arriva via SSE stream, non come return value della chiamata HTTP. |
| 2 | Il circuit breaker deve applicarsi anche alle chiamate di sessione (`ISessionService`)? | Open | Da decidere in fase di implementazione. Per questa spec, la pipeline si applica solo al named client `"opencode"` che è condiviso — quindi si applica a tutte le chiamate HTTP. Documentare come decisione tecnica. |
| 3 | `Last-Event-ID` — il server opencode supporta effettivamente il resume da event ID? | Open | Da verificare durante l'implementazione. Se non supportato, il reconnect ricarica tutti i messaggi via `GetMessagesAsync` come fallback. |
| 4 | Deserializzazione `MessageUpdatedEvent.Message` — `OpencodeEventDto.Data` è `JsonElement?`. Il payload è un `MessageWithPartsDto` completo? | Open | Da verificare sul wire format opencode. `ChatEventParser` deve gestire il caso in cui la struttura non corrisponda restituendo `UnknownEvent`. |

---

## Acceptance Criteria

> Each criterion maps to one or more functional requirements.

- [ ] **[AC-001]** Dato un testo utente, quando si chiama `IChatService.SendPromptAsync`, allora viene costruito un `SendPromptRequest` valido e inviato via `SendPromptAsyncNoWait`; il risultato è `ChatServiceResult.IsSuccess = true` se il server risponde 204. *(REQ-001, REQ-016)*
- [ ] **[AC-002]** Dato un `sessionId` valido, quando si chiama `IChatService.GetMessagesAsync`, allora viene restituita la lista di `MessageWithPartsDto` wrapped in `ChatServiceResult.Ok`. *(REQ-002)*
- [ ] **[AC-003]** Dato un errore di rete durante `SendPromptAsync`, quando la resilienza è configurata, allora vengono effettuati fino a 3 retry con backoff esponenziale prima di restituire `ChatServiceResult.Fail` con `Kind = NetworkError`. *(REQ-013, REQ-014)*
- [ ] **[AC-004]** Dato il circuit breaker aperto, quando si chiama `SendPromptAsync` o `GetMessagesAsync`, allora viene restituito immediatamente `ChatServiceResult.Fail` con `Kind = CircuitOpen` senza effettuare chiamate HTTP. *(REQ-015)*
- [ ] **[AC-005]** Dato uno stream SSE attivo, quando il server invia un evento `message.updated`, allora `SubscribeToEventsAsync` yield un `MessageUpdatedEvent` con il messaggio deserializzato. *(REQ-007, REQ-009, REQ-010)*
- [ ] **[AC-006]** Dato uno stream SSE interrotto, quando il `CancellationToken` non è cancellato, allora `ChatService` tenta il reconnect con backoff esponenziale includendo `Last-Event-ID` se disponibile. *(REQ-011)*
- [ ] **[AC-007]** Dato un tipo di evento SSE non riconosciuto, quando `ChatEventParser` lo processa, allora viene restituito un `UnknownEvent` con `RawType` e `RawData` preservati, senza eccezioni. *(REQ-010)*
- [ ] **[AC-008]** Dato un testo plain, quando si chiama `SendPromptRequestBuilder.FromText`, allora il `SendPromptRequest` risultante contiene `Parts` con un singolo elemento JSON `{ "type": "text", "text": "<testo>" }`. *(REQ-016)*
- [ ] **[AC-009]** Dato `IsConnected = true`, quando lo stream SSE si interrompe, allora `IsConnected` diventa `false` e l'evento `IsConnectedChanged` viene raised con valore `false`. *(REQ-004, REQ-011)*
- [ ] **[AC-010]** Dato un `CancellationToken` cancellato, quando `SubscribeToEventsAsync` è in esecuzione, allora il loop di reconnect termina e il `IAsyncEnumerable` completa senza eccezioni. *(REQ-011, REQ-012)*

---

## Notes for Technical Analysis

> This section is addressed to the agent that will perform the technical implementation analysis.

- **`Microsoft.Extensions.Http.Resilience`**: aggiungere a `openMob.Core.csproj`. Configurare via `.AddResilienceHandler("opencode-resilience", builder => { ... })` sul named client `"opencode"` in `CoreServiceExtensions`. Verificare compatibilità con .NET 10 / la versione più recente del package.
- **SSE e resilienza**: la pipeline di resilienza standard NON deve avvolgere le request SSE long-lived (il timeout di 30s la chiuderebbe). Valutare se usare un secondo named client `"opencode-sse"` senza timeout per le sole request SSE, oppure configurare un `HttpClient` separato estratto da `IHttpClientFactory` con timeout `Timeout.InfiniteTimeSpan`.
- **`ChatEvent` gerarchia**: usare una classe base astratta (non interfaccia) per permettere pattern matching con `switch` expression. Tutti i derived type sono `sealed record`.
- **`ChatEventParser`**: classe `internal sealed`. Usa `System.Text.Json` per deserializzare `JsonElement` nei typed DTO. Wrappa ogni deserializzazione in try/catch e restituisce `UnknownEvent` in caso di errore.
- **`ChatServiceResult<T>`**: non usare eccezioni per il flusso di errore. Il ViewModel consuma il result type e decide come presentare l'errore all'utente.
- **Singleton lifecycle**: `ChatService` è Singleton perché mantiene stato (`IsConnected`, `_lastEventId`). Verificare thread-safety per l'accesso a `_lastEventId` (usare `Interlocked` o `lock`).
- **Backoff SSE reconnect**: implementare con `Task.Delay` + `CancellationToken`. Non usare Polly per il reconnect SSE — la logica è custom e Polly non gestisce `IAsyncEnumerable`.
- **As established in `specs/done/2026-03-15-02-opencode-api-client.md`**: `OpencodeApiClient` usa `IHttpClientFactory` con named client `"opencode"`. La pipeline di resilienza si configura su quel named client registration in `CoreServiceExtensions`.
- **Wire format `SendPromptRequest.Parts`**: verificare sul server opencode il formato esatto di una text part. Il builder deve produrre JSON compatibile con il TypeScript type `TextPart` del server.

---

## Technical Analysis

> Added by: om-orchestrator | Date: 2026-03-16

### Change Classification

| Field | Value |
|-------|-------|
| Change type | Feature |
| Git Flow branch | feature/chat-service-layer |
| Branches from | develop |
| Estimated complexity | High |
| Estimated agents involved | om-mobile-core, om-tester, om-reviewer |

### Layers Involved

| Layer | Agent | Scope |
|-------|-------|-------|
| Business logic / Services | om-mobile-core | `src/openMob.Core/Services/` |
| Models / Domain types | om-mobile-core | `src/openMob.Core/Models/` |
| Helpers / Parsers | om-mobile-core | `src/openMob.Core/Helpers/` |
| DI configuration | om-mobile-core | `src/openMob.Core/Infrastructure/DI/` |
| Unit Tests | om-tester | `tests/openMob.Tests/` |
| Code Review | om-reviewer | all of the above |

> Note: No XAML/UI work is required for this spec. `om-mobile-ui` is not involved.

### Files to Create

- `src/openMob.Core/Services/IChatService.cs` — public interface with SendPromptAsync, GetMessagesAsync, SubscribeToEventsAsync, IsConnected, IsConnectedChanged
- `src/openMob.Core/Services/ChatService.cs` — Singleton implementation; delegates to IOpencodeApiClient; manages SSE reconnect loop and IsConnected state
- `src/openMob.Core/Models/ChatEventType.cs` — enum with 8 values (ServerConnected, MessageUpdated, MessagePartUpdated, SessionUpdated, SessionError, PermissionRequested, PermissionUpdated, Unknown)
- `src/openMob.Core/Models/ChatEvent.cs` — abstract base class + 8 sealed record derived types (ServerConnectedEvent, MessageUpdatedEvent, MessagePartUpdatedEvent, SessionUpdatedEvent, SessionErrorEvent, PermissionRequestedEvent, PermissionUpdatedEvent, UnknownEvent)
- `src/openMob.Core/Models/ChatServiceResult.cs` — sealed record ChatServiceResult<T> with Ok/Fail factory methods
- `src/openMob.Core/Models/ChatServiceError.cs` — sealed record ChatServiceError + ChatServiceErrorKind enum
- `src/openMob.Core/Helpers/ChatEventParser.cs` — internal sealed class; parses OpencodeEventDto → ChatEvent using System.Text.Json; never throws
- `src/openMob.Core/Helpers/SendPromptRequestBuilder.cs` — sealed class with static FromText() method

### Files to Modify

- `src/openMob.Core/openMob.Core.csproj` — add `Microsoft.Extensions.Http.Resilience` PackageReference
- `src/openMob.Core/Infrastructure/DI/CoreServiceExtensions.cs` — (a) add `.AddResilienceHandler(...)` on the `"opencode"` named client; (b) register `IChatService` → `ChatService` as Singleton; (c) add a second named client `"opencode-sse"` with `Timeout.InfiniteTimeSpan` for SSE requests

### Technical Dependencies

- `IOpencodeApiClient` — already implemented; `ChatService` consumes it via constructor injection
- `IOpencodeConnectionManager` — already implemented; `ChatService` needs it to pass `Last-Event-ID` header on reconnect (the SSE client must be created fresh per reconnect attempt)
- `OpencodeEventDto` — already defined in `Infrastructure/Http/Dtos/Opencode/OpencodeEventDto.cs`; `ChatEventParser` consumes it
- `MessageWithPartsDto`, `PartDto`, `SessionDto` — already defined; used as payload types in typed ChatEvent records
- `SendPromptRequest` — already defined in `Infrastructure/Http/Dtos/Opencode/Requests/SendPromptRequest.cs`; `SendPromptRequestBuilder` constructs it
- `Microsoft.Extensions.Http.Resilience` NuGet package — new dependency, must be added to `openMob.Core.csproj`

### Technical Risks

1. **SSE + Resilience pipeline conflict**: The 30-second request timeout in the resilience pipeline would terminate long-lived SSE connections. Resolution: use a separate named client `"opencode-sse"` (registered without resilience handler, with `Timeout.InfiniteTimeSpan`) exclusively for SSE requests in `ChatService`. The `"opencode"` named client retains the full resilience pipeline for all regular HTTP calls.

2. **Circuit breaker detection in ChatService**: `Microsoft.Extensions.Http.Resilience` throws `BrokenCircuitException` (from `Polly`) when the circuit is open. `ChatService` must catch `Polly.CircuitBreaker.BrokenCircuitException` (or its base `Polly.ExecutionRejectedException`) and map it to `ChatServiceErrorKind.CircuitOpen`. This requires a reference to Polly types — which are transitively available via `Microsoft.Extensions.Http.Resilience`.

3. **Thread-safety of `_lastEventId` and `IsConnected`**: `ChatService` is Singleton and `SubscribeToEventsAsync` can be called from multiple consumers. Use `volatile` for `_isConnected` and `Interlocked.Exchange` / `lock` for `_lastEventId` (string). Alternatively, restrict to single-consumer contract and document it.

4. **`IOpencodeApiClient` is registered as Transient**: `ChatService` (Singleton) cannot inject `IOpencodeApiClient` directly — it would capture a single Transient instance for its lifetime. Resolution: inject `IHttpClientFactory` and `IOpencodeConnectionManager` directly into `ChatService` and construct HTTP calls inline, OR inject `IServiceProvider` and resolve `IOpencodeApiClient` per-call. **Preferred approach**: inject `IOpencodeApiClient` directly — since `ChatService` is the sole consumer of the SSE stream and the prompt/message calls, a single captured instance is acceptable. Document this as an intentional deviation from the Transient registration pattern.

5. **`Last-Event-ID` header on reconnect**: `IOpencodeApiClient.SubscribeToEventsAsync` does not accept a `lastEventId` parameter. `ChatService` must either (a) call `IOpencodeApiClient` with a wrapper that injects the header, or (b) bypass `IOpencodeApiClient` for SSE and use `IHttpClientFactory` directly with the `"opencode-sse"` client. **Decision**: `ChatService` uses `IHttpClientFactory` directly for SSE reconnect to have full control over headers. It replicates the SSE parsing logic from `OpencodeApiClient` or extracts it to a shared helper.

6. **Open Question #2 (circuit breaker scope)**: The resilience pipeline on the `"opencode"` named client applies to ALL callers of that client, including `ISessionService`, `IProjectService`, etc. This is the correct behavior — document it as an ADR.

### Execution Order

> Steps that can run in parallel are marked with ⟳. Steps that must be sequential are numbered.

1. **[Git Flow]** Create branch `feature/chat-service-layer`
2. **[om-mobile-core]** Implement all new files in this order:
   - a. `ChatEventType.cs` and `ChatEvent.cs` (domain model — no dependencies)
   - b. `ChatServiceResult.cs` and `ChatServiceError.cs` (result types — no dependencies)
   - c. `ChatEventParser.cs` (depends on ChatEvent hierarchy and OpencodeEventDto)
   - d. `SendPromptRequestBuilder.cs` (depends on SendPromptRequest)
   - e. `IChatService.cs` (interface — depends on ChatEvent, ChatServiceResult, MessageWithPartsDto)
   - f. `ChatService.cs` (implementation — depends on all of the above + IOpencodeApiClient)
   - g. Modify `openMob.Core.csproj` (add resilience package)
   - h. Modify `CoreServiceExtensions.cs` (add resilience pipeline + register IChatService + add "opencode-sse" client)
3. **[om-tester]** Write unit tests (after om-mobile-core completes)
4. **[om-reviewer]** Full review against spec
5. **[Fix loop if needed]** Address Critical and Major findings
6. **[Git Flow]** Finish branch and merge

### Definition of Done

- [ ] All `[REQ-001]` through `[REQ-017]` requirements implemented
- [ ] All `[AC-001]` through `[AC-010]` acceptance criteria satisfied
- [ ] Unit tests written for `ChatService`, `ChatEventParser`, `SendPromptRequestBuilder`
- [ ] `om-reviewer` verdict: ✅ Approved or ⚠️ Approved with remarks
- [ ] `dotnet build openMob.sln` — zero errors, zero warnings
- [ ] Git Flow branch finished and deleted
- [ ] Spec moved to `specs/done/` with Completed status
