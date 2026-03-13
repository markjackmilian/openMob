---
name: git-flow
description: Git and Git Flow operations for the openMob project. Covers fetch, pull, push (with explicit user confirmation), commit with Conventional Commits format, and the full Git Flow lifecycle — feature, release, hotfix, and support branches. Provides exact shell commands to execute via the bash tool.
license: MIT
compatibility: opencode
metadata:
  workflow: git-flow-nvie
  convention: conventional-commits
  branch-main: main
  branch-integration: develop
---

## What I do

I provide the exact shell commands to perform Git and Git Flow operations on the openMob repository. I cover day-to-day operations (fetch, pull, commit, push) and the full Git Flow branch lifecycle (feature, release, hotfix, support).

## When to use me

Load this skill whenever you need to:
- Commit, fetch, pull, or push changes
- Start, publish, or finish a Git Flow branch
- Create a release or hotfix with correct tagging
- Follow the Conventional Commits format for commit messages

---

## Project Branch Configuration

```
Main branch:         main        (production-ready, tagged releases)
Integration branch:  develop     (integration of completed features)
Feature prefix:      feature/
Release prefix:      release/
Hotfix prefix:       hotfix/
Support prefix:      support/
Bugfix prefix:       bugfix/
Version tags:        v{major}.{minor}.{patch}   (e.g. v1.2.0)
```

---

## Safety Rules — Read Before Every Operation

- **Never** run `git push --force` on `main` or `develop`. These branches are protected.
- **Never** run `git reset --hard` without showing the command to the user and waiting for explicit confirmation.
- **Never** commit files that may contain secrets (`.env`, `appsettings.*.json` with values, `user-secrets`, credentials of any kind).
- **Always** run `git fetch --all --prune` before starting any new operation that involves a remote branch.
- **Always** present `git push` commands to the user for confirmation — never execute a push automatically.
- **Always** verify the working tree is clean (`git status`) before starting a Git Flow operation.

---

## Conventional Commits — Required Format

Every commit message **must** follow this format:

```
<type>(<scope>): <short description>

[optional body]

[optional footer: BREAKING CHANGE: ...]
```

### Types

| Type | When to use |
|------|------------|
| `feat` | New feature or user-visible capability |
| `fix` | Bug fix |
| `chore` | Maintenance, dependency updates, config changes |
| `docs` | Documentation only |
| `refactor` | Code restructure without functional change |
| `test` | Adding or modifying tests |
| `style` | Formatting, XAML style-only changes (no logic) |
| `perf` | Performance improvement |
| `ci` | CI/CD pipeline changes |
| `build` | Build system or project file changes |

### Scopes (recommended)

Use the layer or module name as scope:

```
core       — business logic, services
ui         — XAML, styles, components
auth       — authentication / secrets
sessions   — session management
db         — EF Core, migrations, repositories
tests      — test project
deps       — NuGet dependencies
config     — app configuration
```

### Examples

```bash
git commit -m "feat(sessions): add async session creation with cancellation support"
git commit -m "fix(core): handle null response from opencode API health check"
git commit -m "test(sessions): add error path coverage for SessionService"
git commit -m "chore(deps): update CommunityToolkit.Mvvm to 8.3.0"
git commit -m "refactor(ui): extract CardView as reusable ContentView component"
git commit -m "style(ui): migrate hardcoded colors to StaticResource tokens"
git commit -m "docs: add XML documentation to ISessionService"

# Breaking change
git commit -m "feat(core)!: replace IOpenCodeApiClient with typed HttpClient factory

BREAKING CHANGE: IOpenCodeApiClient interface has changed. All consumers must be updated."
```

---

## Base Operations

### Inspect current state

```bash
# Working tree status
git status

# Compact log with graph (last 20 commits across all branches)
git log --oneline --graph --decorate --all -20

# Diff of unstaged changes
git diff

# Diff of staged changes
git diff --cached

# List all local and remote branches
git branch -a

# List tags sorted by version descending
git tag -l --sort=-v:refname | head -20
```

### Fetch and pull

```bash
# Fetch all remotes, prune deleted remote branches
git fetch --all --prune

# Pull current branch with rebase (preferred — avoids merge commits)
git pull --rebase origin <current-branch>

# Pull develop
git pull --rebase origin develop

# Pull main
git pull --rebase origin main
```

### Staging and committing

```bash
# Review what changed before staging
git status
git diff

# Stage specific files (preferred over git add .)
git add <path/to/file>
git add src/Services/SessionService.cs

# Interactive staging (stage specific hunks within a file)
git add -p

# Stage all tracked changes (only after reviewing git diff)
git add -u

# Commit with Conventional Commits message
git commit -m "feat(sessions): add create session command"

# Amend last commit message (only if NOT yet pushed to remote)
git commit --amend -m "fix(sessions): correct commit message"

# Amend last commit adding currently staged files (only if NOT yet pushed)
git commit --amend --no-edit
```

### Push — always confirm with user first

```bash
# Push current branch (first push — sets upstream)
git push -u origin <branch-name>

# Push current branch (subsequent pushes)
git push origin <branch-name>

# Push develop
git push origin develop

# Push main and tags after a release or hotfix
git push origin main develop --tags
```

> ⚠️ Present all push commands to the user and wait for explicit confirmation before executing.

---

## Git Flow — Feature Branch

Use for new features and non-critical changes. Always branched from `develop`.

### Start a feature

```bash
# Using git-flow CLI
git flow feature start <feature-name>

# Manual equivalent
git fetch --all --prune
git checkout develop
git pull --rebase origin develop
git checkout -b feature/<feature-name>
```

### Work on the feature

```bash
# Stage and commit following Conventional Commits
git add <files>
git commit -m "feat(<scope>): <description>"

# Keep feature branch up to date with develop
git fetch origin develop
git rebase origin/develop
```

### Publish feature to remote (for collaboration or backup)

```bash
# Using git-flow CLI
git flow feature publish <feature-name>

# Manual equivalent — confirm with user before running
git push -u origin feature/<feature-name>
```

### Finish a feature (merge into develop)

```bash
# Ensure the feature is up to date first
git fetch --all --prune
git rebase origin/develop

# Using git-flow CLI (merges into develop, deletes local branch)
git flow feature finish <feature-name>

# Manual equivalent
git checkout develop
git pull --rebase origin develop
git merge --no-ff feature/<feature-name> -m "feat(<scope>): merge feature/<feature-name> into develop"
git branch -d feature/<feature-name>

# Push develop — confirm with user before running
git push origin develop

# Delete remote feature branch — confirm with user before running
git push origin --delete feature/<feature-name>
```

---

## Git Flow — Release Branch

Use to prepare a new production release. Branched from `develop`. Only bug fixes, documentation, and version bumps go into a release branch — no new features.

### Start a release

```bash
# Using git-flow CLI
git flow release start <version>      # e.g. 1.2.0

# Manual equivalent
git fetch --all --prune
git checkout develop
git pull --rebase origin develop
git checkout -b release/<version>
```

### Work on the release

```bash
# Only bug fixes and release preparation commits
git commit -m "fix(<scope>): <description>"
git commit -m "chore(config): bump version to <version>"
```

### Finish a release (merge into main + develop, tag)

```bash
# Using git-flow CLI
git flow release finish <version>
# This will:
# 1. Merge release/<version> into main
# 2. Tag main with v<version>
# 3. Merge release/<version> back into develop
# 4. Delete the release branch

# Manual equivalent
git checkout main
git pull --rebase origin main
git merge --no-ff release/<version> -m "chore(release): merge release/<version> into main"
git tag -a v<version> -m "Release v<version>"

git checkout develop
git pull --rebase origin develop
git merge --no-ff release/<version> -m "chore(release): merge release/<version> back into develop"

git branch -d release/<version>

# Push everything — confirm with user before running each command
git push origin main
git push origin develop
git push origin --tags
git push origin --delete release/<version>
```

---

## Git Flow — Hotfix Branch

Use for critical production bug fixes. Branched directly from `main`, merged back into both `main` and `develop`.

### Start a hotfix

```bash
# Using git-flow CLI
git flow hotfix start <version>       # e.g. 1.1.1

# Manual equivalent
git fetch --all --prune
git checkout main
git pull --rebase origin main
git checkout -b hotfix/<version>
```

### Work on the hotfix

```bash
# Only the targeted fix — minimum scope
git commit -m "fix(<scope>): <description of the critical fix>"
git commit -m "chore(config): bump version to <version>"
```

### Finish a hotfix (merge into main + develop, tag)

```bash
# Using git-flow CLI
git flow hotfix finish <version>
# This will:
# 1. Merge hotfix/<version> into main
# 2. Tag main with v<version>
# 3. Merge hotfix/<version> into develop
# 4. Delete the hotfix branch

# Manual equivalent
git checkout main
git pull --rebase origin main
git merge --no-ff hotfix/<version> -m "fix: merge hotfix/<version> into main"
git tag -a v<version> -m "Hotfix v<version>"

git checkout develop
git pull --rebase origin develop
git merge --no-ff hotfix/<version> -m "fix: merge hotfix/<version> into develop"

git branch -d hotfix/<version>

# Push everything — confirm with user before running each command
git push origin main
git push origin develop
git push origin --tags
git push origin --delete hotfix/<version>
```

---

## Git Flow — Support Branch

Use for long-term maintenance of older versions when `main` has already moved ahead.

```bash
# Start a support branch from a specific tag
git flow support start <support-name> <base-tag>
# e.g.: git flow support start 1.x v1.0.0

# Manual equivalent
git fetch --all --prune
git checkout -b support/<support-name> v<base-tag>

# Push support branch — confirm with user before running
git push -u origin support/<support-name>
```

Support branches are long-lived. Apply fixes directly to the support branch and tag them independently.

---

## Useful Inspection Commands

```bash
# Show commits in current branch not yet in develop
git log develop..HEAD --oneline

# Show diff between current branch and develop
git diff develop...HEAD

# Show diff between two branches
git diff <branch-a>..<branch-b>

# Show who last modified each line of a file
git blame <path/to/file>

# Show full history of a file including renames
git log --follow -p <path/to/file>

# Find which commit introduced a string
git log -S "<search string>" --oneline

# Show details of a specific commit
git show <commit-hash>
```

---

## Stash — Temporary Work Parking

```bash
# Save current uncommitted work with a description
git stash push -m "wip: <brief description>"

# List all stashes
git stash list

# Apply most recent stash (keeps it in list)
git stash apply

# Apply and remove most recent stash
git stash pop

# Apply a specific stash
git stash apply stash@{2}

# Drop a specific stash
git stash drop stash@{0}

# Clear all stashes
git stash clear
```

---

## Commit Checklist

Before every commit, verify:

- [ ] `git status` shows only intended files staged
- [ ] No `.env`, `appsettings.*.json` with secrets, or credential files staged
- [ ] `git diff --cached` reviewed — no debug code, no hardcoded values, no commented-out blocks
- [ ] Commit message follows Conventional Commits format
- [ ] If on a feature branch: branch is up to date with `develop` via rebase

Before every push, verify:

- [ ] User has explicitly confirmed the push
- [ ] Correct branch and remote target
- [ ] `git log origin/<branch>..HEAD` shows only the intended commits
- [ ] No `--force` on `main` or `develop`
