# ADR: opencode prompt_async Wire Format — Nested Model Object

## Date
2026-03-25

## Status
Accepted

## Context
When implementing per-message model override in the openMob app, the initial implementation sent `modelID` and `providerID` as flat top-level fields in the `POST /session/{id}/prompt_async` request body. The server silently accepted the request (HTTP 204) but ignored these fields, always using the model configured server-side for the agent.

Investigation via adb logcat confirmed the fields were being sent correctly by the app. Reading the opencode server source code (`SessionPrompt.PromptInput` zod schema in `packages/opencode/src/session/prompt.ts`) revealed the actual expected wire format.

## Decision
The opencode server's `prompt_async` (and `/session/{id}/message`) endpoints expect the model as a **nested object**, not as flat fields:

```json
{
  "parts": [...],
  "model": {
    "providerID": "anthropic",
    "modelID": "claude-3-5-haiku-20241022"
  },
  "agent": "plan"
}
```

The `SendPromptRequest` C# record was updated to use a nested `SendPromptModelRef` record instead of flat `ModelId`/`ProviderId` string properties.

## Rationale
The opencode SDK (`@opencode-ai/sdk`) `SessionChatParams` type also uses a flat `modelID`/`providerID` structure, which was misleading. However, the actual server-side zod schema (`PromptInput`) uses a nested `model: { providerID, modelID }` object. The server source is authoritative.

The `agent` field remains a flat string (wire name `"agent"`), which is correct and confirmed working.

## Alternatives Considered
- **Keep flat fields**: The server silently ignores them — model override would never work.
- **Use `mode` instead of `agent`**: The SDK uses `mode` in `SessionChatParams`, but the server source shows `agent` is the correct field name for `PromptInput`. Both `mode` and `agent` appear in SSE responses as aliases.

## Consequences
### Positive
- Model override per message now works correctly.
- `SendPromptRequest` accurately reflects the server's expected wire format.
- `SendPromptModelRef` is a typed record that prevents partial construction (both `providerID` and `modelID` must be provided together).

### Negative / Trade-offs
- The `SendPromptRequestBuilder.FromText` method silently produces `Model: null` if only one of `modelId`/`providerId` is provided (both are required). This is intentional but callers must be aware.

## Related Features
fix-model-agent-override-send

## Related Agents
om-mobile-core, om-tester
