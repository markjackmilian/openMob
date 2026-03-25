---
description: Execute a development task unattended — from spec or inline prompt. Creates a Git Flow branch, implements the task via specialist agents, makes intermediate commits, and pushes the branch on completion. Fully autonomous, no manual steps required.
agent: om-orchestrator
subtask: false
---

# om-unattended-dev

You are running in **fully unattended mode**. You must complete the entire development cycle autonomously — from branch creation to final push — without asking the user for confirmation at any step, **except for the single explicit exceptions listed below**.

---

## Input

The task to execute is provided as: `$ARGUMENTS`

Interpret `$ARGUMENTS` as follows:

- **If it ends with `.md`** → treat it as a spec file path (relative to the workspace root). Read the file and use it as the functional specification.
- **If it is any other string** → treat it as an inline task description. Proceed directly to implementation without a formal spec file.
- **If empty** → scan `specs/todo/` for the oldest unprocessed spec file (lowest date prefix) and use that.

---

## Unattended Execution Rules

In this mode, the following standard om-orchestrator rules are **suspended**:

| Standard rule | Unattended override |
|---------------|---------------------|
| Present `git push` to user and wait for confirmation | **Execute automatically** after all checks pass |
| Present `git flow finish` to user and wait for confirmation | **Execute automatically** after all checks pass |
| Wait for manual verification sign-off before merge | **Replaced by automated checks** (see Automated Gate below) |
| Ask user to clarify ambiguous specs | **Make a reasonable assumption, document it in Open Questions, proceed** |

The **only two moments** where you must pause and wait for user input are:

1. **Critical blocker**: `dotnet build` fails with errors that cannot be auto-fixed after 2 fix attempts.
2. **Test failures**: `dotnet test` fails with failures that cannot be auto-fixed after 2 fix attempts.

In all other cases, proceed autonomously.

---

## Execution Phases

### Phase 0 — Resolve Input

1. Parse `$ARGUMENTS` using the rules above.
2. If a spec file path is provided, read it with the Read tool.
3. If an inline description is provided, synthesize a minimal internal spec (title, requirements, acceptance criteria) before proceeding. Do not write this to disk.
4. If `$ARGUMENTS` is empty, list `specs/todo/` and pick the oldest file.

Announce to the session: `[om-unattended-dev] Starting unattended task: <task title>`

---

### Phase 1 — Technical Analysis

Perform the full om-orchestrator Phase 1 Technical Analysis:

- Classify the change type (feature / bugfix / hotfix / release)
- Determine the Git Flow branch name: `feature/<slug>` (default for most tasks)
- Identify layers involved, files to create/modify, dependencies, risks
- Define execution order

If the input is a spec file in `specs/todo/`:
- Append the Technical Analysis section to the spec
- Move it to `specs/in-progress/`

If the input is an inline description:
- Keep the analysis in memory only — do not write spec files to disk

---

### Phase 2 — Git Flow Branch Setup

Load the `git-flow` skill. Execute the following commands **automatically** (no user confirmation needed in unattended mode):

```bash
git fetch --all --prune
git status
```

If the working tree is not clean, stash uncommitted changes:
```bash
git stash push -m "wip: auto-stash before om-unattended-dev"
```

Create the branch:
```bash
git flow feature start <slug>
# or: git checkout -b feature/<slug> develop
```

Announce: `[om-unattended-dev] Branch feature/<slug> created. Starting implementation.`

---

### Phase 3 — Implementation

Dispatch work to specialist agents in the correct order (following om-orchestrator Phase 3 briefs):

1. **@om-mobile-core** — interfaces, services, ViewModels, entities, DI registration
2. **@om-mobile-ui** — XAML pages, components, styles (after ViewModel binding surface is defined)
3. **@om-tester** — unit tests for all new Services and ViewModels

#### Intermediate commits

After each agent completes its work, **immediately commit** using Conventional Commits format:

```bash
git add -u
git add src/   # or specific paths
git commit -m "<type>(<scope>): <description>"
```

Commit cadence:
- After om-mobile-core completes → `feat(<scope>): implement <feature> services and ViewModels`
- After om-mobile-ui completes → `feat(ui): implement <feature> XAML views and components`
- After om-tester completes → `test(<scope>): add unit tests for <feature>`

Do not batch all work into a single commit. Each agent's output gets its own commit.

---

### Phase 4 — Automated Review

Dispatch **@om-reviewer** with a full review brief covering all files created/modified.

If the reviewer returns `🔴 Changes required`:
- Execute the fix loop (om-orchestrator Phase 4) **automatically**
- Re-dispatch the responsible agent with a targeted fix brief
- Re-run the reviewer
- Repeat up to **3 fix iterations**
- If still not approved after 3 iterations, pause and report to the user

After each fix round, commit the changes:
```bash
git add -u
git commit -m "fix(<scope>): address review findings [<finding-ids>]"
```

---

### Phase 5 — Automated Gate

Run the automated verification checks **without user interaction**:

```bash
dotnet build openMob.sln
```

If build fails:
- Attempt auto-fix (dispatch @om-mobile-core with the build error)
- Retry build
- If still failing after 2 attempts → **PAUSE** and report to user

```bash
dotnet test tests/openMob.Tests/openMob.Tests.csproj
```

If tests fail:
- Attempt auto-fix (dispatch @om-tester with the failure details)
- Retry tests
- If still failing after 2 attempts → **PAUSE** and report to user

If both checks pass, announce: `[om-unattended-dev] Automated gate passed. Build ✅ Tests ✅`

---

### Phase 6 — Push Branch

After the automated gate passes, push the feature branch to remote **automatically**:

```bash
git push -u origin feature/<slug>
```

Announce: `[om-unattended-dev] Branch feature/<slug> pushed to remote.`

> Note: In unattended mode, the branch is **pushed but NOT merged**. The `git flow feature finish` (merge into develop) is intentionally left for the user to trigger manually via `@om-orchestrator`, after their own manual verification on device/simulator. This preserves the human sign-off gate for production merges.

---

### Phase 7 — Spec Closure (if applicable)

If the input was a spec file from `specs/in-progress/`:
- Update the Metadata table: set `Status` to `Ready for Review`, add `Branch` field
- Write the updated spec back to `specs/in-progress/`

If the input was an inline description:
- No spec file to update

---

### Phase 8 — Completion Report

Always end with a structured completion report:

```
---

## om-unattended-dev — Completion Report

### Task
<task title or inline description>

### Branch
feature/<slug> — pushed to origin

### What was done
- [ ] Technical analysis completed
- [ ] Branch created: feature/<slug>
- [ ] om-mobile-core: <summary of what was implemented>
- [ ] om-mobile-ui: <summary of what was implemented>
- [ ] om-tester: <summary of tests written>
- [ ] om-reviewer: <verdict>
- [ ] Build: ✅ / ❌
- [ ] Tests: ✅ / ❌ (<N> passed, <N> failed)
- [ ] Branch pushed: ✅

### Commits made
<list of commit hashes and messages>

### Next steps for the user
1. Pull the branch: `git fetch --all && git checkout feature/<slug>`
2. Run the app on simulator or device and verify the feature manually
3. When satisfied, trigger merge: invoke `@om-orchestrator` and say "finish feature <slug>"

### Open questions / assumptions made
<list any assumptions made due to ambiguous input, or open questions that need human resolution>

---
```

---

## Error Handling

| Situation | Action |
|-----------|--------|
| Spec file not found at given path | Report error, list available specs in `specs/todo/`, stop |
| `specs/todo/` is empty and no arguments given | Report "No pending specs found. Provide a spec path or inline description." and stop |
| Git working tree dirty and stash fails | Report error, stop |
| Branch already exists | Checkout the existing branch and continue from where it left off |
| Agent returns an error or incomplete output | Retry once, then report to user if still failing |
| Build fails after 2 auto-fix attempts | Pause, report exact error to user, wait for instruction |
| Tests fail after 2 auto-fix attempts | Pause, report failing test names and errors to user, wait for instruction |
| Reviewer not approved after 3 fix iterations | Pause, report all remaining findings to user, wait for instruction |
