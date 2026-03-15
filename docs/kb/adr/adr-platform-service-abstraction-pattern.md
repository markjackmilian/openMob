# ADR: Platform Service Abstraction Pattern (Interface in Core, Implementation in MAUI)

## Date
2026-03-15

## Status
Accepted

## Context
The opencode Server Connection Model feature needed to store passwords securely using platform-specific APIs (iOS Keychain via SecureStorage, Android EncryptedSharedPreferences). The `openMob.Core` library has zero MAUI dependencies by design, so it cannot reference `Microsoft.Maui.Storage.SecureStorage` directly. A pattern was needed to abstract platform services while keeping Core testable.

## Decision
Define the service interface (`IServerCredentialStore`) in `openMob.Core/Infrastructure/Security/`. Implement the concrete class (`MauiServerCredentialStore`) in the `openMob` MAUI project under `Infrastructure/Security/`. Register the MAUI implementation in `MauiProgram.cs` as a singleton, not in `CoreServiceExtensions.AddOpenMobCore()`.

This follows the existing `IAppDataPathProvider` / `MauiAppDataPathProvider` pattern established during project scaffolding.

## Rationale
- Keeps `openMob.Core` free of MAUI dependencies (testable with plain .NET)
- Allows unit tests to mock `IServerCredentialStore` via NSubstitute or in-memory test doubles
- Platform-specific registration in `MauiProgram.cs` is the only place where MAUI types are referenced
- Consistent with the existing `IAppDataPathProvider` pattern — establishes this as the standard

## Alternatives Considered
- **Thin wrapper in Core with conditional compilation**: Rejected — would introduce `#if` directives and MAUI package references in Core
- **Service Locator pattern**: Rejected — violates DI-only constraint in AGENTS.md
- **Abstract base class instead of interface**: Rejected — interfaces are preferred for NSubstitute mocking

## Consequences
### Positive
- All platform abstractions follow a single, predictable pattern
- Core library remains a pure .NET class library
- Test doubles are trivial to implement

### Negative / Trade-offs
- Platform-specific services cannot be registered in `AddOpenMobCore()` — they must be registered separately in `MauiProgram.cs`
- Spec REQ-010 wording ("registered in AddOpenMobCore") is technically inaccurate for platform services — future specs should account for this split

## Related Features
opencode-server-connection-model

## Related Agents
om-mobile-core, om-tester
