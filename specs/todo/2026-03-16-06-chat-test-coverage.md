# Chat Test Coverage — Unit Test per ViewModel, Converter e Service Layer

## Metadata
| Field   | Value                        |
|---------|------------------------------|
| Date    | 2026-03-16                   |
| Status  | Draft                        |
| Version | 1.0                          |

---

## Executive Summary

Aggiunge la copertura di unit test completa per tutti i componenti introdotti dalle Spec 02, 04 e 05: `ChatService`, `ChatViewModel`, `FlyoutViewModel`, i tre converter (`BoolToVisibilityConverter`, `DateTimeToRelativeStringConverter`, `MessageStatusToIconConverter`) e `ChatEventParser`. Tutti i test seguono il pattern Arrange/Act/Assert con NSubstitute per il mocking e FluentAssertions per le asserzioni. Nessun test introduce dipendenze reali (no HTTP, no DB, no MAUI platform APIs).

---

## Scope

### In Scope
- `ChatServiceTests` — test per `ChatService.SendPromptAsync`, `GetMessagesAsync`, `SubscribeToEventsAsync` (reconnect, cancellazione)
- `ChatViewModelTests` — test per tutti i comandi e le proprietà osservabili di `ChatViewModel`
- `FlyoutViewModelTests` — test per `LoadSessionsCommand`, `SelectSessionCommand`, `DeleteSessionCommand`, `NewChatCommand`
- `BoolToVisibilityConverterTests` — test per tutti i casi di conversione
- `DateTimeToRelativeStringConverterTests` — test per tutti i formati di data
- `MessageStatusToIconConverterTests` — test per tutti i valori di `MessageDeliveryStatus`
- `ChatEventParserTests` — test per la deserializzazione di ogni tipo di evento SSE
- `ChatMessageTests` — test per `ChatMessage.FromDto` e calcolo `TextContent`
- `SendPromptRequestBuilderTests` — test per `SendPromptRequestBuilder.FromText`

### Out of Scope
- Test di integrazione (HTTP reale, SSE reale)
- Test di UI / XAML (non testabili da `openMob.Tests`)
- Test di `MauiProgram.cs` DI wiring
- Test di EF Core migrations
- Test di `OpencodeApiClient` (già coperti in `OpencodeApiClientTests.cs`)
- Test di `SessionService` (già coperti in `SessionServiceTests.cs`)

---

## Functional Requirements

> Requirements are numbered for traceability.

### Struttura e Convenzioni

1. **[REQ-001]** Tutti i file di test devono risiedere in `tests/openMob.Tests/` nella sottocartella corrispondente al tipo testato:
   - `tests/openMob.Tests/Services/ChatServiceTests.cs`
   - `tests/openMob.Tests/ViewModels/ChatViewModelTests.cs`
   - `tests/openMob.Tests/ViewModels/FlyoutViewModelTests.cs`
   - `tests/openMob.Tests/Converters/BoolToVisibilityConverterTests.cs`
   - `tests/openMob.Tests/Converters/DateTimeToRelativeStringConverterTests.cs`
   - `tests/openMob.Tests/Converters/MessageStatusToIconConverterTests.cs`
   - `tests/openMob.Tests/Helpers/ChatEventParserTests.cs`
   - `tests/openMob.Tests/Models/ChatMessageTests.cs`
   - `tests/openMob.Tests/Helpers/SendPromptRequestBuilderTests.cs`

2. **[REQ-002]** Naming convention metodi: `MethodUnderTest_WhenCondition_ExpectedBehavior`. Struttura Arrange/Act/Assert con righe vuote di separazione. Ogni test asserisce esattamente un comportamento.

3. **[REQ-003]** Usare `TestDataBuilder` in `tests/openMob.Tests/Helpers/` per costruire fixture di test riutilizzabili (es. `MessageWithPartsDtoBuilder`, `SessionDtoBuilder`, `OpencodeEventDtoBuilder`).

### ChatServiceTests

4. **[REQ-004]** `SendPromptAsync_WhenClientReturnsSuccess_ReturnsOkResult` — mock `IOpencodeApiClient.SendPromptAsyncNoWait` restituisce `OpencodeResult.Ok(true)`, verifica `ChatServiceResult.IsSuccess == true`.

5. **[REQ-005]** `SendPromptAsync_WhenClientReturnsNetworkError_ReturnsFailResult` — mock restituisce `OpencodeResult` con errore di rete, verifica `ChatServiceResult.IsSuccess == false` e `Error.Kind == NetworkError`.

6. **[REQ-006]** `SendPromptAsync_WhenCancellationRequested_ReturnsCancelledResult` — cancella il token durante la chiamata, verifica `Error.Kind == Cancelled`.

7. **[REQ-007]** `GetMessagesAsync_WhenClientReturnsMessages_ReturnsMappedMessages` — mock restituisce lista di `MessageWithPartsDto`, verifica che il risultato contenga gli stessi messaggi.

8. **[REQ-008]** `GetMessagesAsync_WhenClientReturnsServerError_ReturnsFailResult` — mock restituisce errore 500, verifica `Error.Kind == ServerError` e `Error.HttpStatusCode == 500`.

9. **[REQ-009]** `SubscribeToEventsAsync_WhenStreamYieldsEvents_YieldsMappedChatEvents` — mock `IOpencodeApiClient.SubscribeToEventsAsync` restituisce sequenza di `OpencodeEventDto`, verifica che `SubscribeToEventsAsync` yield i corrispondenti `ChatEvent` typed.

10. **[REQ-010]** `SubscribeToEventsAsync_WhenCancellationRequested_CompletesGracefully` — cancella il token, verifica che il `IAsyncEnumerable` completi senza eccezioni.

11. **[REQ-011]** `IsConnected_WhenServerConnectedEventReceived_BecomesTrue` — verifica che `IsConnected` diventi `true` dopo il primo `ServerConnectedEvent`.

12. **[REQ-012]** `IsConnected_WhenStreamInterrupted_BecomesFalse` — simula interruzione stream, verifica `IsConnected == false` e `IsConnectedChanged` raised con `false`.

### ChatViewModelTests

13. **[REQ-013]** `LoadMessagesCommand_WhenServiceReturnsMessages_PopulatesMessagesCollection` — mock `IChatService.GetMessagesAsync` restituisce messaggi, verifica `Messages.Count > 0` e `IsBusy == false`.

14. **[REQ-014]** `LoadMessagesCommand_WhenServiceReturnsError_SetsErrorMessage` — mock restituisce errore, verifica `ErrorMessage != null` e `IsBusy == false`.

15. **[REQ-015]** `LoadMessagesCommand_WhenExecuted_SetsBusyTrueThenFalse` — verifica la sequenza `IsBusy: false → true → false` durante l'esecuzione.

16. **[REQ-016]** `IsEmpty_WhenMessagesCollectionIsEmpty_IsTrue` — verifica `IsEmpty == true` con collezione vuota.

17. **[REQ-017]** `IsEmpty_WhenMessageAdded_BecomesFalse` — aggiunge un messaggio, verifica `IsEmpty == false`.

18. **[REQ-018]** `SendMessageCommand_WhenExecuted_ClearsInputTextImmediately` — imposta `InputText`, esegue il comando, verifica `InputText == string.Empty` prima del completamento async.

19. **[REQ-019]** `SendMessageCommand_WhenExecuted_AddsOptimisticMessageWithSendingStatus` — verifica che un `ChatMessage` con `DeliveryStatus == Sending` venga aggiunto a `Messages`.

20. **[REQ-020]** `SendMessageCommand_WhenServiceFails_SetsDeliveryStatusToError` — mock `IChatService.SendPromptAsync` restituisce errore, verifica `DeliveryStatus == Error` sul messaggio ottimistico.

21. **[REQ-021]** `SendMessageCommand_CanExecute_WhenInputTextEmpty_ReturnsFalse` — verifica `CanExecute == false` con `InputText` vuoto.

22. **[REQ-022]** `SendMessageCommand_CanExecute_WhenIsAiResponding_ReturnsFalse` — imposta `IsAiResponding = true`, verifica `CanExecute == false`.

23. **[REQ-023]** `CancelResponseCommand_WhenExecuted_SetsIsAiRespondingFalse` — imposta `IsAiResponding = true`, esegue il comando, verifica `IsAiResponding == false`.

24. **[REQ-024]** `SetSession_WhenSessionIdChanges_LoadsMessages` — chiama `SetSession` con nuovo ID, verifica che `LoadMessagesCommand` venga invocato e `SessionId` aggiornato.

25. **[REQ-025]** `OnMessageUpdatedEvent_WhenNewMessage_AddsToCollection` — simula `MessageUpdatedEvent` via subscription mock, verifica che il messaggio venga aggiunto a `Messages`.

26. **[REQ-026]** `OnMessageUpdatedEvent_WhenExistingMessage_UpdatesTextContent` — simula aggiornamento di un messaggio già in `Messages`, verifica `TextContent` aggiornato.

27. **[REQ-027]** `OnSessionErrorEvent_WhenReceived_SetsErrorMessageAndStopsResponding` — simula `SessionErrorEvent`, verifica `ErrorMessage != null` e `IsAiResponding == false`.

28. **[REQ-028]** `RecalculateGrouping_WhenTwoConsecutiveUserMessages_SecondIsNotFirstInGroup` — aggiunge due messaggi utente consecutivi, verifica `Messages[1].IsFirstInGroup == false`.

29. **[REQ-029]** `RecalculateGrouping_WhenUserThenAssistantMessage_BothAreFirstInGroup` — aggiunge messaggio utente poi assistente, verifica entrambi `IsFirstInGroup == true`.

30. **[REQ-030]** `SelectSuggestionChipCommand_WhenExecuted_SetsInputTextAndSendsMessage` — esegue il comando con un chip, verifica `InputText == chip.PromptText` e `SendMessageCommand` invocato.

31. **[REQ-031]** `DismissErrorCommand_WhenExecuted_ClearsErrorMessage` — imposta `ErrorMessage`, esegue il comando, verifica `ErrorMessage == null`.

32. **[REQ-032]** `Dispose_WhenCalled_CancelsSseSubscription` — verifica che il `CancellationToken` della subscription SSE venga cancellato.

### FlyoutViewModelTests

33. **[REQ-033]** `LoadSessionsCommand_WhenServiceReturnsData_PopulatesSessionsCollection` — mock `ISessionService.GetSessionsAsync` restituisce sessioni, verifica `Sessions.Count > 0`.

34. **[REQ-034]** `LoadSessionsCommand_WhenServiceReturnsError_SetsErrorState` — mock restituisce errore, verifica stato di errore.

35. **[REQ-035]** `DeleteSessionCommand_WhenExecuted_RemovesSessionFromCollection` — aggiunge sessione, esegue delete, verifica rimozione da `Sessions`.

36. **[REQ-036]** `NewChatCommand_WhenExecuted_CreatesNewSessionAndNavigates` — mock `ISessionService.CreateSessionAsync` restituisce nuova sessione, verifica navigazione a `//chat?sessionId=<newId>`.

37. **[REQ-037]** `SelectSessionCommand_WhenExecuted_NavigatesToChatWithSessionId` — esegue il comando con una sessione, verifica navigazione a `//chat?sessionId=<id>`.

### Converter Tests

38. **[REQ-038]** `BoolToVisibilityConverterTests`:
    - `Convert_WhenTrue_ReturnsTrue`
    - `Convert_WhenFalse_ReturnsFalse`
    - `Convert_WhenTrueWithInvertParameter_ReturnsFalse`
    - `Convert_WhenNullValue_ReturnsFalse`

39. **[REQ-039]** `DateTimeToRelativeStringConverterTests`:
    - `Convert_WhenToday_ReturnsHHmm`
    - `Convert_WhenYesterday_ReturnsYesterdayHHmm`
    - `Convert_WhenOlderDate_ReturnsMmmDdHHmm`
    - `Convert_WhenNullValue_ReturnsEmptyString`
    - `Convert_WhenDateTimeOffset_FormatsCorrectly`

40. **[REQ-040]** `MessageStatusToIconConverterTests`:
    - `Convert_WhenSending_ReturnsClockIcon`
    - `Convert_WhenSent_ReturnsCheckIcon`
    - `Convert_WhenError_ReturnsErrorIcon`
    - `Convert_WhenUnknownValue_ReturnsEmptyString`

### ChatEventParserTests

41. **[REQ-041]** `Parse_WhenServerConnectedEvent_ReturnsServerConnectedEvent` — input `OpencodeEventDto` con `EventType = "server.connected"`, verifica output `ServerConnectedEvent`.

42. **[REQ-042]** `Parse_WhenMessageUpdatedEvent_ReturnsMessageUpdatedEventWithMessage` — input con payload `MessageWithPartsDto` serializzato, verifica `MessageUpdatedEvent.Message` correttamente deserializzato.

43. **[REQ-043]** `Parse_WhenSessionErrorEvent_ReturnsSessionErrorEventWithMessage` — verifica `SessionErrorEvent.ErrorMessage` estratto correttamente.

44. **[REQ-044]** `Parse_WhenUnknownEventType_ReturnsUnknownEventWithRawData` — input con tipo non riconosciuto, verifica `UnknownEvent.RawType` e `RawData` preservati.

45. **[REQ-045]** `Parse_WhenMalformedPayload_ReturnsUnknownEventWithoutThrowing` — input con JSON malformato nel payload, verifica nessuna eccezione e output `UnknownEvent`.

### ChatMessageTests

46. **[REQ-046]** `FromDto_WhenUserMessage_SetsIsFromUserTrue` — input `MessageWithPartsDto` con `Role = "user"`, verifica `IsFromUser == true`.

47. **[REQ-047]** `FromDto_WhenAssistantMessage_SetsIsFromUserFalse` — input con `Role = "assistant"`, verifica `IsFromUser == false`.

48. **[REQ-048]** `FromDto_WhenMultipleTextParts_ConcatenatesTextContent` — input con due parti `type = "text"`, verifica `TextContent` concatenato.

49. **[REQ-049]** `FromDto_WhenNoTextParts_ReturnsEmptyTextContent` — input senza parti testo, verifica `TextContent == string.Empty`.

50. **[REQ-050]** `FromDto_WhenTimestampProvided_ParsesCorrectly` — verifica `Timestamp` correttamente estratto dal campo `time.created` in ms Unix.

### SendPromptRequestBuilderTests

51. **[REQ-051]** `FromText_WhenTextProvided_CreatesSingleTextPart` — verifica `Parts.Count == 1` e il JSON contiene `"type": "text"` e `"text": "<input>"`.

52. **[REQ-052]** `FromText_WhenModelIdProvided_SetsModelId` — verifica `ModelId == <input>`.

53. **[REQ-053]** `FromText_WhenNoModelId_ModelIdIsNull` — verifica `ModelId == null`.

---

## Functional Impacts

### Affected Components / Systems
| Component | Impact | Notes |
|-----------|--------|-------|
| `tests/openMob.Tests/Services/ChatServiceTests.cs` | Nuovo file | |
| `tests/openMob.Tests/ViewModels/ChatViewModelTests.cs` | Nuovo file | |
| `tests/openMob.Tests/ViewModels/FlyoutViewModelTests.cs` | Nuovo file | |
| `tests/openMob.Tests/Converters/BoolToVisibilityConverterTests.cs` | Nuovo file | |
| `tests/openMob.Tests/Converters/DateTimeToRelativeStringConverterTests.cs` | Nuovo file | |
| `tests/openMob.Tests/Converters/MessageStatusToIconConverterTests.cs` | Nuovo file | |
| `tests/openMob.Tests/Helpers/ChatEventParserTests.cs` | Nuovo file | |
| `tests/openMob.Tests/Models/ChatMessageTests.cs` | Nuovo file | |
| `tests/openMob.Tests/Helpers/SendPromptRequestBuilderTests.cs` | Nuovo file | |
| `tests/openMob.Tests/Helpers/TestDataBuilders.cs` | Modifica | Aggiunta builder per `MessageWithPartsDto`, `OpencodeEventDto`, `SessionDto` |

### Dependencies
- **Spec 02** — `ChatService`, `IChatService`, `ChatEvent`, `ChatEventParser`, `SendPromptRequestBuilder` devono esistere
- **Spec 04** — `ChatViewModel`, `ChatMessage`, `MessageDeliveryStatus`, `SuggestionChip` devono esistere
- **Spec 05** — Converter in `openMob.Core.Converters` devono esistere (spostati/creati)
- `FlyoutViewModel` — già esistente, test da aggiungere
- NSubstitute 5.x, FluentAssertions 6.x, xUnit 2.x — già referenziati

---

## Open Questions & Clarifications

| # | Question | Status | Answer / Decision |
|---|----------|--------|-------------------|
| 1 | `ChatViewModel` usa `IDispatcher` per marshalling UI — come mockarlo nei test? | Resolved | NSubstitute: `var dispatcher = Substitute.For<IDispatcher>(); dispatcher.When(d => d.Dispatch(Arg.Any<Action>())).Do(ci => ci.Arg<Action>()());` — esegue l'action inline nel thread di test. |
| 2 | `FlyoutViewModel` usa navigazione Shell — come testare `NewChatCommand` e `SelectSessionCommand` senza MAUI? | Resolved | Iniettare `INavigationService` (interfaccia custom) nel ViewModel invece di chiamare `Shell.Current.GoToAsync` direttamente. Mockare `INavigationService` nei test. |
| 3 | I test SSE di `ChatService` richiedono `IAsyncEnumerable` mock — NSubstitute supporta questo? | Resolved | Sì. Usare `AsyncEnumerable.Create` o un `Channel<T>` per simulare lo stream. Alternativa: creare un `FakeOpencodeApiClient` helper nel progetto test. |
| 4 | `ChatViewModel.SubscribeToEventsAsync` gira in background — come testare gli effetti degli eventi SSE in modo sincrono? | Resolved | Usare `TaskCompletionSource` e `await Task.Delay` con timeout breve nei test, oppure esporre un metodo `internal` `ProcessEvent(ChatEvent)` testabile direttamente (con `[InternalsVisibleTo("openMob.Tests")]`). |

---

## Acceptance Criteria

> Each criterion maps to one or more functional requirements.

- [ ] **[AC-001]** Dato `dotnet test tests/openMob.Tests/openMob.Tests.csproj`, quando eseguito dopo l'implementazione di Spec 02, 04, 05, allora tutti i test definiti in questa spec passano con zero fallimenti. *(tutti i REQ)*
- [ ] **[AC-002]** Dato il report di coverage, quando generato con `coverlet`, allora `ChatService`, `ChatViewModel`, `FlyoutViewModel` e tutti i converter hanno copertura ≥ 80% delle righe. *(REQ-004–REQ-053)*
- [ ] **[AC-003]** Dato un test qualsiasi in questa spec, quando eseguito in isolamento, allora non effettua chiamate HTTP reali, non accede al filesystem, e non usa MAUI platform APIs. *(REQ-002)*
- [ ] **[AC-004]** Dato `ChatViewModelTests`, quando si verifica il test del grouping, allora i casi di messaggi consecutivi stesso mittente e mittente alternato sono entrambi coperti. *(REQ-028, REQ-029)*
- [ ] **[AC-005]** Dato `ChatEventParserTests`, quando si testa un payload malformato, allora nessun test lancia eccezioni non gestite. *(REQ-045)*

---

## Notes for Technical Analysis

> This section is addressed to the agent that will perform the technical implementation analysis.

- **`IDispatcher` mock**: pattern standard per testare ViewModel con dispatching. Aggiungere helper `DispatcherHelper.CreateSynchronous()` in `tests/openMob.Tests/Helpers/` che restituisce un `IDispatcher` NSubstitute che esegue le action inline.
- **`INavigationService`**: se non già definita, `FlyoutViewModel` deve essere refactored per iniettare un'astrazione di navigazione invece di usare `Shell.Current` direttamente. Questo è un prerequisito per la testabilità. Definire `INavigationService` in `openMob.Core` con metodo `Task NavigateToAsync(string route, IDictionary<string, object>? parameters = null)`.
- **`IAsyncEnumerable` mock per SSE**: usare `System.Threading.Channels.Channel<T>` per creare uno stream controllabile nei test. Scrivere eventi nel channel dal test, leggere dal `ChatService` tramite il mock. Esempio:
  ```csharp
  var channel = Channel.CreateUnbounded<OpencodeEventDto>();
  apiClient.SubscribeToEventsAsync(Arg.Any<CancellationToken>())
      .Returns(channel.Reader.ReadAllAsync(ct));
  ```
- **`[InternalsVisibleTo]`**: aggiungere `[assembly: InternalsVisibleTo("openMob.Tests")]` in `openMob.Core/AssemblyInfo.cs` per esporre metodi `internal` ai test (es. `ChatEventParser.Parse`, `ChatViewModel.ProcessEvent`).
- **TestDataBuilders**: creare builder fluenti per i DTO più usati nei test. Esempio:
  ```csharp
  var message = new MessageWithPartsDtoBuilder()
      .WithRole("user")
      .WithTextPart("Hello")
      .Build();
  ```
- **Timeout nei test async**: usare `CancellationTokenSource` con timeout di 5s per tutti i test che attendono eventi asincroni. Evitare `Task.Delay` senza timeout (test che non terminano mai).
- **As established in `AGENTS.md`**: test stack obbligatorio — xUnit 2.x, NSubstitute 5.x, FluentAssertions 6.x. Non sostituire con alternative.
