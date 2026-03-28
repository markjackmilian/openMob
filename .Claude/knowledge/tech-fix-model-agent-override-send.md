# Technical Analysis — Fix: Model & Agent Override Not Applied on Message Send
**Feature slug:** fix-model-agent-override-send
**Completed:** 2026-03-25
**Branch:** bugfix/fix-model-agent-override-send
**Complexity:** Low

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
