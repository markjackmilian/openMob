---
name: local-docs
description: Manages the openMob knowledge base using the local-docs MCP. Use to search past specifications, technical analyses, architecture decisions, and release notes. om-orchestrator indexes completed work at merge time; om-planner queries for context before writing new specs.
license: MIT
compatibility: opencode
metadata:
  project: openMob
  mcp: local-docs
  roles: om-orchestrator writes, om-planner reads
---

## What I do

I provide instructions for interacting with the **openMob knowledge base** stored in the local-docs MCP. The knowledge base accumulates institutional memory across all features: completed specs, technical decisions, architecture choices, and release history.

## When to use me

- **om-planner**: load me before Phase 1 (initial intake) and before Phase 3 (confirmation) to search for relevant past work
- **om-orchestrator**: load me during Phase 5.1 (after merge) to index the completed feature into the knowledge base

---

## Project Setup

The openMob knowledge base lives in a local-docs project named **`openMob`**.

### Step 1 — Verify the project exists

```
mcp_local-docs_list_projects()
```

Look for a project named `openMob` in the results. Note its `ID`.

### Step 2 — Create if missing

If no `openMob` project is found:

```
mcp_local-docs_create_project(
  name: "openMob",
  description: "Knowledge base for the openMob project. Contains completed functional specifications, technical analyses, architecture decision records, and release notes."
)
```

Store the returned project ID for all subsequent operations.

### Step 3 — Retrieve project ID (if already exists)

```
mcp_local-docs_get_project(projectIdOrName: "openMob")
```

Use the returned `id` field in all subsequent calls.

---

## Searching the Knowledge Base

Used by: **om-planner**

Run semantic queries in natural language. The MCP returns the most relevant document chunks.

```
mcp_local-docs_search_docs(
  query: "<natural language query>",
  projectId: "<openMob project ID>",
  limit: 5
)
```

### Query guidelines

Write queries as questions or topic descriptions, not keywords:

```
# Good — natural language, specific
"session management feature requirements and decisions"
"how was opencode API authentication implemented"
"EF Core migration strategy for new entities"
"navigation service abstraction pattern"
"what patterns were used for error handling in ViewModels"

# Less effective — too generic
"sessions"
"API"
"error"
```

### Interpreting results

Each result includes a chunk of text and its source document. Use the results to:
- Understand what has already been built and decided
- Avoid re-specifying or re-deciding things already resolved
- Reference past decisions in new specs ("as established in feature X, we use...")
- Identify potential conflicts or dependencies with past work

### When nothing is found

If the search returns no relevant results, proceed normally. Do not mention the search to the user unless results were found.

---

## Indexing Completed Work

Used by: **om-orchestrator** (Phase 5.1 — after merge only)

Index **four document types** for each completed feature. Not all four are required every time — see conditions below.

---

### Document Type 1 — Completed Specification

**Always index.** Index every feature that reaches `specs/done/`.

```
mcp_local-docs_add_document(
  projectId: "<openMob project ID>",
  fileName: "spec-<slug>.md",
  content: "<full content of specs/done/YYYY-MM-DD-<slug>.md>"
)
```

- `<slug>` is the feature slug from the spec filename (e.g. `session-management`, `user-auth-flow`)
- Content is the complete spec file including the Technical Analysis section appended by om-orchestrator

---

### Document Type 2 — Technical Analysis Extract

**Always index.** Extract only the `## Technical Analysis` section from the completed spec and index it separately for faster retrieval of technical decisions.

```
mcp_local-docs_add_document(
  projectId: "<openMob project ID>",
  fileName: "tech-<slug>.md",
  content: "<extracted ## Technical Analysis section only>"
)
```

Prepend a header before the extracted content:

```markdown
# Technical Analysis — <Feature Title>
**Feature slug:** <slug>
**Completed:** YYYY-MM-DD
**Branch:** feature/<slug>
**Complexity:** Low / Medium / High

---

<paste the ## Technical Analysis section here>
```

---

### Document Type 3 — Release Note

**Index only for `hotfix/*` and `release/*` branches.** Not required for `feature/*` branches.

```
mcp_local-docs_add_document(
  projectId: "<openMob project ID>",
  fileName: "release-<version>.md",
  content: "<release note content>"
)
```

Release note format:

```markdown
# Release v<version> — YYYY-MM-DD

## Type
hotfix / release

## Branch
<branch-name> merged into main + develop

## Tag
v<version>

## Features / Fixes Included

| Slug | Summary |
|------|---------|
| <slug-1> | One-line description of what was done |
| <slug-2> | One-line description of what was done |

## Breaking Changes
None identified. / <description of breaking change>

## Notes
<Any relevant deployment notes, migration steps, or warnings>
```

---

### Document Type 4 — Architecture Decision Record (ADR)

**Index automatically when om-orchestrator detects a significant architectural decision during the feature lifecycle.**

```
mcp_local-docs_add_document(
  projectId: "<openMob project ID>",
  fileName: "adr-<topic-slug>.md",
  content: "<ADR content>"
)
```

#### When to create an ADR automatically

om-orchestrator must propose an ADR when any of the following is true:

| Trigger | Example |
|---------|---------|
| A new public interface is introduced that changes the contract between layers | Introducing `INavigationService` to abstract Shell navigation |
| A new architectural pattern is adopted for the first time | First use of SSE streaming, first use of `SecureStorage` wrapper |
| A new structural NuGet package is added | Adding Polly, SkiaSharp, a new EF Core provider |
| A decision deviates from the standards defined in the agent prompts | Choosing not to use `[ObservableProperty]` for a specific reason |
| A choice is made between two significant alternatives | Choosing typed `HttpClient` over a custom `RestClient` wrapper |
| A cross-cutting constraint is established | "All API calls must include a 30s timeout" |

When a trigger is detected, om-orchestrator must:
1. Announce to the user: "I detected an architectural decision worth recording: [description]. I will create an ADR."
2. Generate the ADR content.
3. Index it to local-docs.

ADR format:

```markdown
# ADR: <Decision Title>

## Date
YYYY-MM-DD

## Status
Accepted

## Context
<What situation or problem prompted this decision. What constraints existed.>

## Decision
<What was decided. Be specific about the chosen approach.>

## Rationale
<Why this option was chosen over alternatives. What trade-offs were accepted.>

## Alternatives Considered
- **<Alternative A>**: <why it was not chosen>
- **<Alternative B>**: <why it was not chosen>

## Consequences
### Positive
- <benefit>

### Negative / Trade-offs
- <cost or limitation>

## Related Features
<slug-1>, <slug-2>

## Related Agents
<which agents are affected by this decision>
```

---

## Updating an Existing Document

If a spec is revised (version 1.1, 1.2, etc.) and re-completed, update the existing document instead of creating a duplicate.

### Step 1 — Find the existing document ID

```
mcp_local-docs_list_documents(projectId: "<openMob project ID>")
```

Look for the document with `fileName` matching `spec-<slug>.md` or `tech-<slug>.md`.

### Step 2 — Update it

```
mcp_local-docs_update_document(
  documentId: "<document ID>",
  content: "<updated content>",
  fileName: "spec-<slug>.md"
)
```

The previous version is preserved as history but removed from search results. Only the new version is searchable.

---

## Listing All Indexed Documents

To inspect what is currently in the knowledge base:

```
mcp_local-docs_list_documents(projectId: "<openMob project ID>")
```

This returns all active documents with their IDs, file names, and metadata.

---

## Quick Reference — MCP Tool Calls

| Operation | MCP Tool | When |
|-----------|----------|------|
| List all projects | `mcp_local-docs_list_projects()` | Verify openMob project exists |
| Create project | `mcp_local-docs_create_project(name, description)` | First-time setup only |
| Get project ID | `mcp_local-docs_get_project(projectIdOrName)` | Before any operation |
| Search knowledge base | `mcp_local-docs_search_docs(query, projectId, limit)` | om-planner Phase 0 and Phase 2 |
| Index new document | `mcp_local-docs_add_document(projectId, fileName, content)` | om-orchestrator Phase 5.1 |
| Update existing document | `mcp_local-docs_update_document(documentId, content, fileName)` | om-orchestrator on spec revision |
| List all documents | `mcp_local-docs_list_documents(projectId)` | Inspection / finding document IDs |
| Get document content | `mcp_local-docs_get_document_content(documentId)` | Retrieve full document for reference |
