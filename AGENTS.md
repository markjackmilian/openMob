# openMob — Agent Guidelines

## Project Overview

openMob is a cross-platform mobile app built with **.NET 10 / .NET MAUI** targeting **iOS and Android**.
The repository follows a strict **specs-first workflow**: all work is driven by specs in `specs/todo/`.

```
openMob.sln
├── src/openMob/            # .NET MAUI app (iOS + Android) — UI + platform glue only
├── src/openMob.Core/       # Pure .NET class library — all business logic lives here
└── tests/openMob.Tests/    # xUnit test project — references openMob.Core only
```

---

## Build Commands

```bash
# Build entire solution
dotnet build openMob.sln

# Build for a specific platform
dotnet build src/openMob/openMob.csproj -f net10.0-android
dotnet build src/openMob/openMob.csproj -f net10.0-ios

# Release build
dotnet build openMob.sln -c Release

# Build Core library only (fastest, no MAUI SDK required)
dotnet build src/openMob.Core/openMob.Core.csproj
```

A clean build **must exit with code 0 and zero warnings** before merging to `develop`.

---

## Test Commands

```bash
# Run all tests
dotnet test tests/openMob.Tests/openMob.Tests.csproj

# Run a single test class
dotnet test tests/openMob.Tests/openMob.Tests.csproj \
  --filter "FullyQualifiedName~SessionListViewModelTests"

# Run a single test method
dotnet test tests/openMob.Tests/openMob.Tests.csproj \
  --filter "FullyQualifiedName~SessionListViewModelTests.LoadSessions_WhenServiceReturnsData_PopulatesCollection"

# Verbose output
dotnet test tests/openMob.Tests/openMob.Tests.csproj \
  --logger "console;verbosity=detailed"
```

**Test stack (mandatory — do not substitute):**
| Concern | Package |
|---|---|
| Runner | xUnit 2.x |
| Mocking | NSubstitute 5.x |
| Assertions | FluentAssertions 6.x |
| Coverage | coverlet.collector |

---

## EF Core Migrations

```bash
# Add a new migration (run from repo root)
dotnet ef migrations add <MigrationName> \
  --project src/openMob.Core/openMob.Core.csproj \
  --startup-project src/openMob/openMob.csproj

# Apply migrations locally
dotnet ef database update \
  --project src/openMob.Core/openMob.Core.csproj \
  --startup-project src/openMob/openMob.csproj
```

Never use `EnsureCreated()` in production code. Migrations are applied at startup via `db.Database.Migrate()`.

---

## Architecture Rules

### Layer Separation (non-negotiable)
- `openMob.Core` has **zero MAUI dependencies** — pure .NET class library.
- `openMob` (MAUI project) handles platform glue and UI only; no business logic.
- `openMob.Tests` references `openMob.Core` **only** — the MAUI project cannot be referenced from tests due to multi-targeting incompatibility.
- All ViewModels, Services, Converters, and Models must live in `openMob.Core`.

### MVVM Pattern
- ViewModels inherit from `ObservableObject` (CommunityToolkit.Mvvm).
- Bindable state: `[ObservableProperty]` source generators.
- Commands: `[RelayCommand]` / `[AsyncRelayCommand]`.
- Views are XAML-only with `x:DataType` compiled bindings. Zero business logic in code-behind.

### Dependency Injection
- DI composition root: `MauiProgram.cs`.
- Core services registered via `AddOpenMobCore()` extension.
- Constructor injection only — no ServiceLocator, no `App.Current.Services`.
- Every injectable service must have a backing interface for testability.

---

## Code Style

### Formatting (enforced by `.editorconfig`)
- Indentation: **4 spaces** for C#; **2 spaces** for XML/XAML/`.csproj`.
- Line endings: **CRLF**.
- Charset: **UTF-8**.
- Files must end with a newline; trailing whitespace trimmed.

### C# Language
- Language version: `preview` (C# 14 preview features).
- Nullable reference types: **enabled** (`<Nullable>enable</Nullable>`).
- File-scoped namespaces: always (`namespace openMob.Core.Data;` not `namespace { }`).
- Sealed by default on leaf classes.
- `async/await` everywhere — never `.Result`, `.Wait()`, or `.GetAwaiter().GetResult()`.
- `ConfigureAwait(false)` in Core library and service code; not needed in ViewModels.
- No `async void` except MAUI lifecycle handlers (`OnAppearing`, etc.).
- No `static` mutable state in ViewModels or Services.

### Naming Conventions
| Element | Convention | Example |
|---|---|---|
| Classes | PascalCase | `ClaudeApiClient`, `AppDbContext` |
| Interfaces | `I` prefix + PascalCase | `IClaudeApiClient`, `IAppDataPathProvider` |
| Private fields | `_camelCase` | `_httpClientFactory`, `_pathProvider` |
| Methods | PascalCase | `GetSessionsAsync`, `AddOpenMobCore` |
| Async methods | `Async` suffix | `CheckHealthAsync`, `CreateSessionAsync` |
| Parameters / locals | camelCase | `httpClientFactory`, `sessionId` |
| DTOs | `Dto` suffix, `sealed record` | `SessionDto`, `MessageDto` |
| Extension classes | `[Target]Extensions` | `CoreServiceExtensions` |
| Helper classes | `[Topic]Helper` | `SentryHelper` |
| XAML color tokens | `Color` prefix + semantic name | `ColorBackground`, `ColorPrimary` |
| XAML spacing tokens | `Spacing` + size | `SpacingXs`, `SpacingLg` |

### Imports
- Use `GlobalUsings.cs` for project-wide `global using` statements.
- Order: System namespaces → Microsoft/third-party → project namespaces.
- No unused `using` statements.

### XML Documentation
- Mandatory `/// <summary>` on all `public` and `internal` members.

### Error Handling
- Use Sentry for monitoring: `SentryHelper.CaptureException(ex, extras)`.
- Sentry DSN must **never be hardcoded** — read from `IConfiguration` / `SecureStorage`.
- Dev secrets: `dotnet user-secrets`. Runtime secrets: MAUI `SecureStorage`.

---

## Testing Guidelines

### Test Structure
- Test method naming: `MethodUnderTest_WhenCondition_ExpectedBehavior`
  - Example: `LoadSessionsCommand_WhenServiceReturnsData_PopulatesSessionsCollection`
- Strict **Arrange / Act / Assert** structure with blank lines between sections.
- Each test asserts exactly one behaviour.
- Use `TestDataBuilder` in `tests/openMob.Tests/Helpers/` for test fixture construction.

### What to Test
- ViewModels, Services, Converters in `openMob.Core`.
- No real external dependencies: no DB I/O, no HTTP calls, no MAUI platform APIs.
- Mock all dependencies with NSubstitute interfaces.
- Assert results with FluentAssertions (`result.Should().Be(...)`).

### What Not to Test
- XAML views or code-behind.
- `MauiProgram.cs` DI wiring.
- EF Core migrations.

---

## Git & Branching

This project uses **Git Flow**:
| Branch | Purpose |
|---|---|
| `main` | Production — tagged releases only |
| `develop` | Integration branch — all features merge here |
| `feature/<slug>` | New feature work |
| `bugfix/<slug>` | Bug fixes on develop |
| `hotfix/<version>` | Emergency patches on main |
| `release/<version>` | Release stabilisation |

**Commit format:** Conventional Commits
```
feat: add session list ViewModel
fix: handle null response from Claude API
chore: update NuGet packages
refactor: extract IRepository abstraction
test: add unit tests for BoolToVisibilityConverter
docs: update AGENTS.md
```

---

## Key Technology Versions

| Technology | Version |
|---|---|
| .NET / C# | 10.0 / preview |
| .NET MAUI | latest stable |
| Target platforms | `net10.0-ios`, `net10.0-android` |
| CommunityToolkit.Mvvm | latest |
| EF Core + SQLite | 9.x |
| Sentry.Maui | latest |
| xUnit | 2.x |
| NSubstitute | 5.x |
| FluentAssertions | 6.x |

---

## Workflow

All development is **spec-driven**:
1. Specs land in `specs/todo/` before any code is written.
2. Agents pick up specs, append Technical Analysis, and move them to `specs/in-progress/`.
3. On merge, specs move to `specs/done/`.

The `.opencode/agents/` directory contains prompt definitions for the specialised AI agents
(`om-mobile-core`, `om-mobile-ui`, `om-tester`, `om-reviewer`, `om-planner`, `om-orchestrator`).
