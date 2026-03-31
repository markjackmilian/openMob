# Server-Side Auto-Approve — Permission Config from Server Management

## Metadata
| Field   | Value                        |
|---------|------------------------------|
| Date    | 2026-03-30                   |
| Status  | Draft                        |
| Version | 1.0                          |

---

## Executive Summary

The opencode server supports a `permission` configuration field (documented at `opencode.ai/docs/permissions`) that can be set to `"allow"` globally, bypassing the `permission.asked` SSE mechanism entirely — the server never asks for approval and tool calls execute without client interaction. This is a **server-side, persistent** solution that complements the existing client-side `AutoAccept` toggle: when the server is configured with `permission: "allow"`, no `permission.asked` events are emitted at all, so the mobile client's connectivity state is irrelevant. This spec adds a dedicated **Auto-Approve** toggle to the Server Management page (specifically the server detail/edit form) that writes `{ "permission": "allow" }` or `{ "permission": "ask" }` to the server's config via `PATCH /config`. The toggle reflects the current server config state and is clearly labelled to distinguish it from the session-level `AutoAccept` toggle in the chat context sheet.

---

## Scope

### In Scope
- Reading the current `permission` value from `GET /config` when the server detail page opens
- Displaying a toggle labelled **"Approva automaticamente (server)"** on the server detail/edit page
- When the toggle is switched ON: calling `PATCH /config` with `{ "permission": "allow" }` and persisting the result
- When the toggle is switched OFF: calling `PATCH /config` with `{ "permission": "ask" }` to restore the default ask behaviour
- Rollback of the toggle state on API failure (same pattern as `ToggleAutoAcceptCommand` in `ContextSheetViewModel`)
- A subtitle or helper text explaining the difference between server-side and session-level auto-approve
- Error message display on failure (non-blocking, same pattern as other settings in the app)

### Out of Scope
- Modifying the session-level `AutoAccept` toggle in the chat context sheet (separate feature, already implemented)
- Granular per-tool permission rules (e.g. `{ "bash": "allow", "edit": "ask" }`) — only the global `"*"` shorthand is in scope
- Reading or displaying the full permission ruleset (complex object syntax) — only the simple string form (`"allow"` / `"ask"`) is handled
- Persisting the server-side permission state locally in the app's SQLite DB (the server is the source of truth)
- Applying the server-side config change to sessions already in progress (the opencode server applies config changes to new tool calls immediately — no session restart needed)
- Changes to the `ServerManagementViewModel` list page (only the detail/edit page is affected)
- Changes to the `AutoAccept` toggle in `ContextSheetViewModel` or `ChatViewModel`

---

## Functional Requirements

> Requirements are numbered for traceability.

1. **[REQ-001]** The server detail/edit ViewModel (`ServerDetailViewModel` or equivalent) MUST expose a new observable property `IsServerAutoApproveEnabled` (bool) that reflects whether the active server's `permission` config is currently set to `"allow"`.

2. **[REQ-002]** When the server detail page loads (or when the user navigates to it), the ViewModel MUST call `IOpencodeApiClient.GetConfigAsync()` and set `IsServerAutoApproveEnabled = true` if the returned config's `permission` field equals `"allow"` (string), `false` otherwise. If `GetConfigAsync` fails or the server is unreachable, `IsServerAutoApproveEnabled` MUST default to `false` and an `ErrorMessage` MUST be set.

3. **[REQ-003]** The ViewModel MUST expose a `ToggleServerAutoApproveCommand` (`[AsyncRelayCommand]`) that:
   a. Captures the previous value of `IsServerAutoApproveEnabled`.
   b. Sets `IsServerAutoApproveEnabled = !IsServerAutoApproveEnabled` optimistically.
   c. Calls `IOpencodeApiClient.UpdateConfigAsync` with the appropriate permission value:
      - Toggle ON → `{ "permission": "allow" }`
      - Toggle OFF → `{ "permission": "ask" }`
   d. On success: clears `ErrorMessage`.
   e. On failure: reverts `IsServerAutoApproveEnabled` to its previous value and sets `ErrorMessage` to a localised error string.

4. **[REQ-004]** The `UpdateConfigAsync` call MUST use the existing `UpdateConfigRequest(JsonElement Config)` DTO. The `JsonElement` MUST be constructed to represent `{ "permission": "allow" }` or `{ "permission": "ask" }` as a minimal JSON object — only the `permission` key is sent, not the full config, to avoid overwriting other server settings.

5. **[REQ-005]** The server detail page XAML MUST display the toggle with:
   - A primary label: **"Approva automaticamente (server)"**
   - A secondary subtitle: **"Bypassa le richieste di permesso per tutti gli strumenti. Valido anche quando l'app è offline."**
   - The toggle bound to `IsServerAutoApproveEnabled` with `ToggleServerAutoApproveCommand` on toggled.
   - A visual separator or section header to distinguish this setting from connection parameters (host, port, credentials).

6. **[REQ-006]** The toggle MUST be disabled (`IsEnabled = false`) while `IsBusy` is `true` (i.e. while the `ToggleServerAutoApproveCommand` is executing) to prevent double-taps.

7. **[REQ-007]** If the server is not reachable when the page loads (e.g. the user is managing a server that is currently offline), the toggle MUST be shown but disabled, with `ErrorMessage` set to **"Impossibile leggere la configurazione del server."**

8. **[REQ-008]** The `IsServerAutoApproveEnabled` state MUST NOT be persisted in the local SQLite database. It is always read from the live server config on page load. If the server is offline, the last-known state is not shown (toggle disabled per REQ-007).

---

## Functional Impacts

### Affected Components / Systems
| Component | Impact | Notes |
|-----------|--------|-------|
| `ServerDetailViewModel.cs` (or equivalent edit ViewModel) | Modified | Add `IsServerAutoApproveEnabled`, `ToggleServerAutoApproveCommand`, `IsBusy`, `ErrorMessage`; inject `IOpencodeApiClient` |
| `ServerDetailPage.xaml` (or equivalent edit page) | Modified | Add toggle row with label, subtitle, and binding |
| `IOpencodeApiClient.cs` | None | `GetConfigAsync` and `UpdateConfigAsync` already exist |
| `UpdateConfigRequest.cs` | None | Already accepts `JsonElement` — no changes needed |
| `ConfigDto.cs` | Possibly modified | Verify that the `Permission` field is deserialised from the GET /config response; if it is currently a raw `JsonElement?`, a typed string property may need to be added for easy reading |

### Dependencies
- `IOpencodeApiClient.GetConfigAsync` — already implemented, returns `ConfigDto`
- `IOpencodeApiClient.UpdateConfigAsync` — already implemented, accepts `UpdateConfigRequest(JsonElement)`
- `ConfigDto.Permission` — must be readable as a string; verify current DTO structure (it may be a raw `JsonElement?`)
- Server management navigation — the detail/edit page is already reachable from `ServerManagementPage` via `NavigateToEditCommand`

---

## Open Questions & Clarifications

| # | Question | Status | Answer / Decision |
|---|----------|--------|-------------------|
| 1 | What is the exact name of the server detail/edit ViewModel and page? The `ServerManagementViewModel` handles the list; there must be a separate ViewModel for the add/edit form. | Open | To be determined during Technical Analysis by inspecting the navigation routes and existing ViewModels. |
| 2 | Does `ConfigDto` currently expose `Permission` as a typed string or as a raw `JsonElement?`? | Open | To be verified during Technical Analysis. If it is a `JsonElement?`, a helper property or explicit deserialisation is needed to read the string value. |
| 3 | Should the toggle also handle the case where `permission` is an object (granular rules) rather than a simple string? | Resolved | No — if the current config has granular rules (object syntax), the toggle reads it as "not allow" (i.e. `false`) and switching it ON replaces the entire permission config with the simple `"allow"` string. Switching it OFF restores `"ask"`. This is an intentional simplification; advanced users can edit the config file directly. |
| 4 | Should the server-side auto-approve state be visible from the chat page (e.g. in the context status bar) to inform the user that the server is in full-allow mode? | Open | Deferred to a future spec. For this spec, the setting is only visible and editable from the server detail page. |
| 5 | What happens if `PATCH /config` is called with only `{ "permission": "allow" }` — does the server merge it with the existing config or replace the entire config? | Resolved | The opencode server's `PATCH /config` endpoint performs a **merge** (confirmed by the server docs and the `UpdateConfigRequest(JsonElement Config)` pattern). Only the `permission` key is overwritten; all other config keys are preserved. |

---

## Acceptance Criteria

> Each criterion maps to one or more functional requirements.

- [ ] **[AC-001]** Given the server detail page opens and the server's `permission` config is `"allow"`, when the page loads, then `IsServerAutoApproveEnabled` is `true` and the toggle is shown as ON. *(REQ-001, REQ-002)*

- [ ] **[AC-002]** Given the server detail page opens and the server's `permission` config is `"ask"` (or any non-`"allow"` value), when the page loads, then `IsServerAutoApproveEnabled` is `false` and the toggle is shown as OFF. *(REQ-001, REQ-002)*

- [ ] **[AC-003]** Given `IsServerAutoApproveEnabled` is `false`, when the user switches the toggle ON, then `PATCH /config` is called with `{ "permission": "allow" }` and on success the toggle remains ON with no error message. *(REQ-003, REQ-004)*

- [ ] **[AC-004]** Given `IsServerAutoApproveEnabled` is `true`, when the user switches the toggle OFF, then `PATCH /config` is called with `{ "permission": "ask" }` and on success the toggle remains OFF with no error message. *(REQ-003, REQ-004)*

- [ ] **[AC-005]** Given `PATCH /config` fails (network error or non-2xx), when the toggle is switched, then `IsServerAutoApproveEnabled` is reverted to its previous value and `ErrorMessage` is set. *(REQ-003e)*

- [ ] **[AC-006]** Given the toggle command is executing (`IsBusy == true`), when the user attempts to tap the toggle again, then the toggle is disabled and no second API call is made. *(REQ-006)*

- [ ] **[AC-007]** Given the server is unreachable when the page loads, when `GetConfigAsync` fails, then the toggle is disabled, `IsServerAutoApproveEnabled` is `false`, and `ErrorMessage` is set to "Impossibile leggere la configurazione del server." *(REQ-007)*

- [ ] **[AC-008]** Given the toggle is ON and the user navigates away and back to the server detail page, when the page reloads, then `GetConfigAsync` is called again and the toggle reflects the current server state. *(REQ-002, REQ-008)*

---

## Notes for Technical Analysis

> This section is addressed to the agent that will perform the technical implementation analysis.

### Key areas to investigate

1. **Server detail/edit ViewModel identification**: Navigate the codebase to find the ViewModel and page used for adding/editing a server connection. The `ServerManagementViewModel` uses `NavigateToEditAsync(ServerConnectionDto dto)` which pushes `"server-detail"` with a `serverId` query param. Find the corresponding ViewModel (likely `ServerDetailViewModel` or `ServerConnectionDetailViewModel`) and page XAML. This is the primary file to modify.

2. **`ConfigDto.Permission` field type**: Inspect `ConfigDto.cs` to determine how the `permission` field is currently deserialised. The opencode server returns `permission` as either a string (`"allow"`, `"ask"`, `"deny"`) or an object (granular rules). If `ConfigDto` stores it as `JsonElement?`, add a helper:
   ```csharp
   public bool IsPermissionAllow =>
       Permission.HasValue &&
       Permission.Value.ValueKind == JsonValueKind.String &&
       Permission.Value.GetString() == "allow";
   ```
   Or add a typed `string? PermissionString` property with a custom converter.

3. **`UpdateConfigRequest` JSON construction**: The `UpdateConfigRequest` takes a `JsonElement`. To construct `{ "permission": "allow" }` as a `JsonElement`:
   ```csharp
   var json = JsonSerializer.SerializeToElement(new { permission = "allow" });
   var request = new UpdateConfigRequest(json);
   ```
   This is the minimal-impact approach — no new DTOs needed.

4. **`IOpencodeApiClient` injection into the server detail ViewModel**: The current `ServerDetailViewModel` (or equivalent) likely only injects `IServerConnectionRepository`, `INavigationService`, and possibly `IOpencodeDiscoveryService`. Adding `IOpencodeApiClient` is a new dependency — update the DI registration in `MauiProgram.cs` accordingly.

5. **`IsBusy` and `ErrorMessage` pattern**: Check whether the server detail ViewModel already has `IsBusy` and `ErrorMessage` observable properties (common pattern across the app). If not, add them following the established pattern from `ContextSheetViewModel` or `SessionListViewModel`.

6. **XAML toggle placement**: The server detail page likely has a form with host, port, username, password fields. Add the auto-approve toggle as a new section below the connection parameters, separated by a `BoxView` divider or a section header `Label`. Follow the existing design system tokens (`ColorSurface`, `ColorOnSurface`, `SpacingMd`, etc.).

### Constraints to respect
- `ConfigureAwait(false)` on all `await` calls in `openMob.Core`.
- No `async void` — `ToggleServerAutoApproveCommand` must be `[AsyncRelayCommand]`.
- Rollback pattern: capture previous value before optimistic update, restore on failure (same as `ToggleAutoAcceptCommand` in `ContextSheetViewModel`).
- The `UpdateConfigAsync` call sends only `{ "permission": "..." }` — never the full config object — to avoid overwriting unrelated server settings.
- Strings in Italian (hardcoded per project convention).
- No local persistence of the server-side permission state.

### Related files or modules
- Server detail ViewModel (path TBD during Technical Analysis)
- Server detail page XAML (path TBD during Technical Analysis)
- `src/openMob.Core/Infrastructure/Http/IOpencodeApiClient.cs` — `GetConfigAsync`, `UpdateConfigAsync` (no changes needed)
- `src/openMob.Core/Infrastructure/Http/Dtos/Opencode/ConfigDto.cs` — verify `Permission` field type
- `src/openMob.Core/Infrastructure/Http/Dtos/Opencode/Requests/UpdateConfigRequest.cs` — no changes needed
- `src/openMob/MauiProgram.cs` — update DI registration for server detail ViewModel

### References to past decisions
- As established in **`session-context-sheet-3of3`**: the `ToggleAutoAcceptCommand` rollback pattern (capture → optimistic update → revert on failure) is the standard for boolean toggles that call async APIs. Apply the same pattern here.
- As established in **`opencode-api-client`** (2026-03-15): all API calls use the `OpencodeResult<T>` pattern via `ExecuteAsync`. `UpdateConfigAsync` follows this pattern — check `result.IsSuccess` before clearing `ErrorMessage`.
- As established in **`server-management-ui`** (2026-03-16): the server management navigation uses `"server-detail"` as the route for the add/edit form. The ViewModel for that route is the primary target of this spec.
- The opencode server `PATCH /config` performs a **merge** (not a replace) — confirmed. Sending only `{ "permission": "allow" }` is safe and will not overwrite other config fields.
