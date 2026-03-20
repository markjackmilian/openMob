# ADR: Agent Picker — Hidden Filter and No Synthetic Default Entry

## Date
2026-03-20

## Status
Accepted

## Context

During implementation of the Session Context Sheet agent selection feature, two product decisions were made that deviate from the original spec and establish patterns for all future agent picker work.

**Problem 1 — Hidden system agents appearing in the picker:**
The opencode `GET /agent` API returns all agents, including system agents (`compaction`, `title`, `summary`) which have `Mode="primary"` but are not intended for user selection. The original spec's filter (`Mode == "primary" | "all"`) was insufficient — these agents would appear in the picker.

**Problem 2 — Synthetic "Default" entry was confusing:**
The spec called for a synthetic first entry labelled "Default" (with `Name=null`) to allow users to reset to the server default. After product review, this was considered confusing because users on opencode desktop see `build` and `plan` as the two primary agents — there is no "Default" concept in the opencode UI. The synthetic entry added complexity without clarity.

## Decision

### 1. Filter `Hidden == true` in `GetPrimaryAgentsAsync`

`AgentDto` was extended with `Hidden bool` (mapped from `"hidden"` in the API response). `GetPrimaryAgentsAsync` filters:

```csharp
all.Where(a => (a.Mode is "primary" or "all") && !a.Hidden)
```

This correctly excludes `compaction`, `title`, `summary` and any future hidden agents.

### 2. No synthetic "Default" entry — show only real agents

The picker shows only real agents returned by `GetPrimaryAgentsAsync`. There is no prepended synthetic entry.

The fallback display name for `null` preference (no agent selected) is `"build"` — the opencode default primary agent — not `"Default"`.

```csharp
// ContextSheetViewModel.cs and ChatViewModel.cs
public string SelectedAgentDisplayName => SelectedAgentName ?? "build";
```

```xml
<!-- AgentPickerSheet.xaml -->
<Label Text="{Binding Name, FallbackValue='build'}" />
```

`ProjectPreference.AgentName = null` continues to mean "use the server default" (which is `build` unless overridden by `default_agent` in `opencode.json`).

## Rationale

- **Hidden filter:** The `hidden` field is the canonical opencode mechanism for marking system agents as non-user-facing. Filtering on it is the correct approach and future-proof.
- **No synthetic entry:** opencode desktop shows only real agents. Matching this UX is more intuitive than introducing an abstract "Default" concept. If the user wants to reset to `build`, they select `build` explicitly.
- **`"build"` as fallback:** `build` is the opencode built-in default primary agent. Using it as the display fallback is accurate and consistent with opencode's own terminology.

## Alternatives Considered

- **Keep synthetic "Default" entry:** Rejected — confusing to users familiar with opencode desktop. Adds complexity to `AgentItem` model (nullable `Name`, `DisplayName` computed property).
- **Filter by agent name blacklist (`compaction`, `title`, `summary`):** Rejected — brittle, breaks if opencode adds new hidden system agents with different names.
- **Use `"Default"` as fallback string:** Rejected — not a real opencode concept. `"build"` is more accurate.

## Consequences

### Positive
- Picker shows only agents the user can meaningfully select.
- `AgentItem.Name` remains `string` (non-nullable) — simpler model.
- Consistent with opencode desktop UX.
- Future-proof: any agent with `Hidden=true` is automatically excluded.

### Negative / Trade-offs
- Users cannot explicitly "reset to default" via the picker (there is no reset entry). If a user wants to go back to `build` after selecting `plan`, they must select `build` explicitly. This is acceptable — `build` is always present in the list.
- `SelectedAgentDisplayName` returns `"build"` for `null` preference. If a server's `default_agent` is configured to something other than `build`, the display name will be misleading. This is a known limitation — a future enhancement could read `default_agent` from the server config.

## Related Features
- session-context-sheet-2of3-agent-model
- session-context-sheet-3of3-thinking-autoaccept-subagent (future — subagent picker uses `GetAgentsAsync`, unfiltered)

## Related Agents
- om-mobile-core: must apply `Hidden` filter in any future agent listing
- om-mobile-ui: `AgentPickerSheet.xaml` uses `{Binding Name, FallbackValue='build'}`
- om-tester: `AgentDto` test helpers must include `Hidden: false` for visible agents, `Hidden: true` for hidden agents
