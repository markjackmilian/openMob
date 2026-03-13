# Spec: Project Architecture Scaffolding

## Metadata

| Field   | Value                        |
|---------|------------------------------|
| Date    | 2026-03-13                   |
| Status  | **In Progress**              |
| Version | 1.0                          |
| Author  | om-orchestrator              |

---

## Summary

Set up the initial solution structure for the openMob project. The solution must contain three projects with a clean separation of concerns: a .NET MAUI app project for the UI layer, a .NET Core class library for all business logic and data access, and an xUnit test project. This scaffolding establishes the architectural foundation for all future features.

---

## Requirements

### REQ-001 — Solution file
Create a `openMob.sln` solution file at the repository root that references all three projects.

### REQ-002 — MAUI App project (`src/openMob/`)
Create a .NET MAUI application project targeting `net10.0-android` and `net10.0-ios`. This project contains:
- `MauiProgram.cs` — DI wiring, Sentry initialisation, EF Core migration apply on startup
- `AppShell.xaml` / `AppShell.xaml.cs` — Shell navigation host
- `App.xaml` / `App.xaml.cs`
- `GlobalUsings.cs` — global using directives for MAUI namespaces
- `Views/Pages/` — empty placeholder directory (`.gitkeep`)
- `Views/Controls/` — empty placeholder directory (`.gitkeep`)
- `ViewModels/` — empty placeholder directory (`.gitkeep`)
- `Converters/` — empty placeholder directory (`.gitkeep`)
- `Resources/Styles/Colors.xaml` — base color palette ResourceDictionary
- `Resources/Styles/Styles.xaml` — base styles ResourceDictionary
- `Resources/Fonts/` — font assets directory
- `Resources/Images/` — image assets directory
- NuGet references: `CommunityToolkit.Mvvm`, `CommunityToolkit.Maui`, `Sentry.Maui`

### REQ-003 — Core class library project (`src/openMob.Core/`)
Create a .NET class library targeting `net10.0`. This project contains all business logic, data access, and infrastructure. It must have **no dependency on MAUI** — it must be pure .NET. Structure:
- `Models/` — EF Core entity POCOs (empty placeholder)
- `Services/` — business logic interfaces and implementations (empty placeholder)
- `Data/AppDbContext.cs` — EF Core DbContext with SQLite provider, configured for `FileSystem.AppDataDirectory` path via constructor injection of a path provider interface
- `Data/Migrations/` — EF Core migrations directory (empty placeholder)
- `Infrastructure/Http/IClaudeApiClient.cs` — typed HTTP client interface
- `Infrastructure/Http/ClaudeApiClient.cs` — typed HTTP client implementation using `IHttpClientFactory`
- `Infrastructure/Monitoring/SentryHelper.cs` — Sentry breadcrumb/capture helpers
- `Infrastructure/DI/CoreServiceExtensions.cs` — `IServiceCollection` extension method `AddOpenMobCore()` that registers all Core services
- `GlobalUsings.cs` — global using directives
- NuGet references: `Microsoft.EntityFrameworkCore.Sqlite`, `Microsoft.EntityFrameworkCore.Design`, `Sentry`, `CommunityToolkit.Mvvm`, `Microsoft.Extensions.Http` (required for `IHttpClientFactory`)

### REQ-004 — Test project (`tests/openMob.Tests/`)
Create an xUnit test project targeting `net10.0`. Structure:
- `ViewModels/` — empty placeholder (`.gitkeep`)
- `Services/` — empty placeholder (`.gitkeep`)
- `Converters/` — empty placeholder (`.gitkeep`)
- `Helpers/TestDataBuilder.cs` — empty static class scaffold
- `GlobalUsings.cs` — global usings for xUnit, NSubstitute, FluentAssertions
- NuGet references: `xunit`, `xunit.runner.visualstudio`, `NSubstitute`, `FluentAssertions`, `Microsoft.NET.Test.Sdk`
- Project references: `openMob.Core` only

> **Architecture note:** The `openMob` (MAUI) project is **not** referenced from `openMob.Tests`. A MAUI app project targeting `net10.0-android;net10.0-ios` cannot be referenced by a plain `net10.0` xUnit project — the multi-targeting mismatch causes build failures. As a consequence, all testable logic (ViewModels, Converters, Services) **must reside in `openMob.Core`** or be kept free of MAUI platform APIs. The `openMob` project contains only MAUI-specific wiring (`MauiProgram.cs`, platform entry points, XAML code-behind) which does not require unit testing.

### REQ-005 — Project references
- `openMob` (MAUI) → references `openMob.Core`
- `openMob.Tests` → references `openMob.Core` only (see architecture note in REQ-004)

### REQ-006 — Solution folders
The solution must organise projects into solution folders:
- `src` folder: `openMob`, `openMob.Core`
- `tests` folder: `openMob.Tests`

### REQ-007 — .gitignore and .editorconfig
- Ensure a `.gitignore` appropriate for .NET MAUI exists at the root (extend existing if present)
- Create a `.editorconfig` at the root enforcing: `indent_style = space`, `indent_size = 4`, `charset = utf-8`, `end_of_line = crlf`, `insert_final_newline = true`
- Exception: XML-family files (`*.xml`, `*.xaml`, `*.csproj`, `*.sln`) use `indent_size = 2` — this is the standard convention for XML and is enforced via a file-type override in `.editorconfig`

### REQ-008 — Nullable and LangVersion
All projects must have:
- `<Nullable>enable</Nullable>`
- `<ImplicitUsings>enable</ImplicitUsings>`
- `<LangVersion>preview</LangVersion>` (C# 14 / .NET 10 preview features)
- `<TreatWarningsAsErrors>false</TreatWarningsAsErrors>` (relaxed during early development)

### REQ-009 — AppDbContext path provider
`AppDbContext` must not reference `FileSystem.AppDataDirectory` directly (that is a MAUI API). Instead, define an interface `IAppDataPathProvider` in `openMob.Core` with a single property `string AppDataPath`. The MAUI project registers a concrete `MauiAppDataPathProvider : IAppDataPathProvider` that returns `FileSystem.AppDataDirectory`. This keeps `openMob.Core` free of MAUI dependencies.

### REQ-010 — IClaudeApiClient scaffold
`IClaudeApiClient` must declare the following method signatures (no implementation required at this stage — bodies throw `NotImplementedException`):
- `Task<bool> CheckHealthAsync(CancellationToken ct = default)`
- `Task<IReadOnlyList<SessionDto>> GetSessionsAsync(CancellationToken ct = default)`
- `Task<SessionDto> CreateSessionAsync(string? title, CancellationToken ct = default)`
- `Task<MessageDto> SendMessageAsync(string sessionId, string content, CancellationToken ct = default)`
- `IAsyncEnumerable<ServerEventDto> StreamEventsAsync(CancellationToken ct = default)`

Define `SessionDto`, `MessageDto`, `ServerEventDto` as simple record types in `Infrastructure/Http/Dtos/`.

### REQ-011 — Solution must build
After scaffolding, `dotnet build openMob.sln` must succeed with zero errors (warnings are acceptable during initial scaffolding).

---

## Acceptance Criteria

### AC-001
`dotnet build openMob.sln` exits with code 0.

### AC-002
`dotnet test tests/openMob.Tests/openMob.Tests.csproj` exits with code 0 (no tests yet, but the project must compile and the runner must initialise).

### AC-003
`openMob.Core` has no direct reference to any `Microsoft.Maui.*` namespace in its source files.

### AC-004
`AppDbContext` is injected with `IAppDataPathProvider` — no direct call to `FileSystem.AppDataDirectory` inside `openMob.Core`.

### AC-005
All new `.cs` files have file-scoped namespaces.

### AC-006
`MauiProgram.cs` calls `AddOpenMobCore()` extension method and applies EF Core migrations on startup.

---

## Open Questions

| # | Question | Status |
|---|----------|--------|
| 1 | Should Tizen be included as a target platform? | **Resolved: No** — iOS and Android only |
| 2 | Should Windows be included as a target platform? | **Resolved: No** — mobile only (iOS + Android) |

---

## Out of Scope

- Any actual feature implementation (sessions, messages, settings, etc.)
- CI/CD pipeline configuration
- App icons and splash screens (placeholder assets only)
- Localisation / resource strings

---

## Technical Analysis

> Added by: om-orchestrator | Date: 2026-03-13

### Change Classification

| Field | Value |
|-------|-------|
| Change type | Feature (initial scaffolding) |
| Git Flow branch | `feature/project-scaffolding` |
| Branches from | `develop` |
| Estimated complexity | Medium |
| Estimated agents involved | om-mobile-core, om-mobile-ui, om-reviewer |

### Layers Involved

| Layer | Agent | Scope |
|-------|-------|-------|
| Solution & project files, DI wiring, Core infrastructure | om-mobile-core | `src/openMob.Core/`, `src/openMob/MauiProgram.cs`, `*.csproj`, `*.sln` |
| XAML shell, resources, styles | om-mobile-ui | `src/openMob/AppShell.xaml`, `src/openMob/Resources/` |
| Code Review | om-reviewer | all of the above |

> Note: `om-tester` is **not** involved in this scaffolding feature — no business logic to test yet. The test project itself is created by `om-mobile-core`.

### Files to Create

**Solution root:**
- `openMob.sln` — solution file with solution folders
- `.editorconfig` — code style enforcement

**`src/openMob/` (MAUI App):**
- `src/openMob/openMob.csproj`
- `src/openMob/MauiProgram.cs`
- `src/openMob/App.xaml` + `App.xaml.cs`
- `src/openMob/AppShell.xaml` + `AppShell.xaml.cs`
- `src/openMob/GlobalUsings.cs`
- `src/openMob/Infrastructure/MauiAppDataPathProvider.cs`
- `src/openMob/Views/Pages/.gitkeep`
- `src/openMob/Views/Controls/.gitkeep`
- `src/openMob/ViewModels/.gitkeep`
- `src/openMob/Converters/.gitkeep`
- `src/openMob/Resources/Styles/Colors.xaml`
- `src/openMob/Resources/Styles/Styles.xaml`
- `src/openMob/Resources/Fonts/.gitkeep`
- `src/openMob/Resources/Images/.gitkeep`

**`src/openMob.Core/` (Class Library):**
- `src/openMob.Core/openMob.Core.csproj`
- `src/openMob.Core/GlobalUsings.cs`
- `src/openMob.Core/Models/.gitkeep`
- `src/openMob.Core/Services/.gitkeep`
- `src/openMob.Core/Data/AppDbContext.cs`
- `src/openMob.Core/Data/IAppDataPathProvider.cs`
- `src/openMob.Core/Data/Migrations/.gitkeep`
- `src/openMob.Core/Infrastructure/Http/IClaudeApiClient.cs`
- `src/openMob.Core/Infrastructure/Http/ClaudeApiClient.cs`
- `src/openMob.Core/Infrastructure/Http/Dtos/SessionDto.cs`
- `src/openMob.Core/Infrastructure/Http/Dtos/MessageDto.cs`
- `src/openMob.Core/Infrastructure/Http/Dtos/ServerEventDto.cs`
- `src/openMob.Core/Infrastructure/Monitoring/SentryHelper.cs`
- `src/openMob.Core/Infrastructure/DI/CoreServiceExtensions.cs`

**`tests/openMob.Tests/` (xUnit):**
- `tests/openMob.Tests/openMob.Tests.csproj`
- `tests/openMob.Tests/GlobalUsings.cs`
- `tests/openMob.Tests/Helpers/TestDataBuilder.cs`
- `tests/openMob.Tests/ViewModels/.gitkeep`
- `tests/openMob.Tests/Services/.gitkeep`
- `tests/openMob.Tests/Converters/.gitkeep`

### Files to Modify

- `.gitignore` — extend with .NET MAUI patterns if not already present

### Technical Dependencies

- .NET 10 SDK (confirmed available: 10.0.103)
- MAUI workloads: `android`, `ios` (confirmed installed)
- NuGet packages (all must resolve at build time):
  - `CommunityToolkit.Mvvm` (latest stable)
  - `CommunityToolkit.Maui` (latest stable)
  - `Sentry.Maui` (latest stable)
  - `Microsoft.EntityFrameworkCore.Sqlite` (latest stable compatible with .NET 10)
  - `Microsoft.EntityFrameworkCore.Design` (latest stable)
  - `Sentry` (latest stable)
  - `xunit` 2.x
  - `xunit.runner.visualstudio`
  - `NSubstitute` 5.x
  - `FluentAssertions` 6.x
  - `Microsoft.NET.Test.Sdk`

### Technical Risks

- **MAUI + .NET 10:** MAUI on .NET 10 is the current release; template generation via `dotnet new maui` should work. The generated `.csproj` may include Tizen/Windows targets that must be removed.
- **openMob.Tests referencing MAUI project:** xUnit test projects cannot run MAUI platform code. The reference to `openMob` is needed only for ViewModels and Converters (pure C# classes). Any MAUI-specific code in the app project that leaks into test compilation will cause build failures. This is a known risk — mitigated by keeping ViewModels and Converters free of MAUI platform APIs.
- **EF Core + .NET 10:** EF Core 9.x is the latest stable; EF Core 10 preview may be needed. Use latest stable EF Core 9.x unless a .NET 10-compatible EF Core 10 package is available.
- **`dotnet new maui` output:** The template generates boilerplate files (MainPage, etc.) that must be cleaned up and replaced with the architecture-compliant structure.

### Execution Order

> Steps that can run in parallel are marked with ⟳.

1. **[Git Flow]** Create branch `feature/project-scaffolding` from `develop`
2. **[om-mobile-core]** Generate solution, create all three `.csproj` files, scaffold all Core and infrastructure C# files, wire DI in `MauiProgram.cs`, create test project scaffold
3. ⟳ **[om-mobile-ui]** Create `AppShell.xaml`, `App.xaml`, `Colors.xaml`, `Styles.xaml` — can run in parallel with step 2 once the MAUI project directory exists (coordinate with om-mobile-core)
4. **[om-reviewer]** Full review against spec — runs after both step 2 and step 3 complete
5. **[Fix loop if needed]** Address Critical and Major findings
6. **[Git Flow]** Finish branch and merge into `develop`

### Definition of Done

- [x] All open questions resolved (no Tizen, no Windows)
- [ ] REQ-001 through REQ-011 implemented
- [ ] AC-001: `dotnet build openMob.sln` exits 0
- [ ] AC-002: `dotnet test` exits 0
- [ ] AC-003: no `Microsoft.Maui.*` in `openMob.Core`
- [ ] AC-004: `IAppDataPathProvider` pattern in place
- [ ] AC-005: file-scoped namespaces everywhere
- [ ] AC-006: `AddOpenMobCore()` called in `MauiProgram.cs`
- [ ] `om-reviewer` verdict: ✅ Approved or ⚠️ Approved with remarks
- [ ] Git Flow branch finished and deleted
- [ ] Spec moved to `specs/done/` with Completed status
