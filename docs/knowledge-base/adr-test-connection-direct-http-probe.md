# ADR: Test Connection Uses Direct HTTP Probe via IHttpClientFactory

## Date
2026-03-16

## Status
Accepted

## Context
`ServerDetailPage` has a "Test Connection" button that should verify whether the URL entered in the form is reachable. The initial implementation used `IOpencodeApiClient.GetHealthAsync()`, which internally resolves the base URL from `IOpencodeConnectionManager.GetBaseUrlAsync()` — i.e. it always probes the **currently active server**, not the URL in the form. This caused a bug where entering a fake URL (e.g. `https://test.com`) still returned "Connected" because the real active server was healthy.

## Decision
`TestConnectionCommand` in `ServerDetailViewModel` uses `IHttpClientFactory.CreateClient("opencode")` to make a direct `GET {formUrl}/global/health` HTTP call, where `formUrl` is constructed from the URL field parsed via `ServerUrlHelper.TryParse`. The response JSON is parsed manually to extract `healthy` and `version` fields.

`IOpencodeApiClient` is **not** used for this operation.

## Rationale
- `IOpencodeApiClient.GetHealthAsync()` is bound to the active server via `IOpencodeConnectionManager` — it cannot probe an arbitrary URL.
- `IHttpClientFactory` is already registered and available in Core (`AddHttpClient()` in `CoreServiceExtensions`).
- The named client `"opencode"` has no pre-configured base address or timeout, making it suitable for ad-hoc probes with a caller-controlled CTS (10 seconds).
- `IOpencodeDiscoveryService.ValidateServerAsync()` was also considered but hardcodes `http://` (no HTTPS support) and uses a 5-second internal timeout.

## Alternatives Considered
- **`IOpencodeApiClient.GetHealthAsync()`**: Probes active server, not form URL. Rejected — causes the bug described above.
- **`IOpencodeDiscoveryService.ValidateServerAsync()`**: Hardcodes HTTP scheme (HTTPS servers always fail), 5-second internal timeout overrides the 10-second spec requirement. Rejected.
- **Temporarily set form server as active, test, restore**: Complex, has side effects on `ConnectionStatus` and `StatusBannerView`. Rejected.

## Consequences
### Positive
- Test Connection accurately reflects the URL the user is editing.
- HTTPS URLs are supported correctly.
- 10-second timeout is fully controlled by the ViewModel.
- No side effects on the active server or connection state.

### Negative / Trade-offs
- `ServerDetailViewModel` is the only ViewModel in the project that directly injects `IHttpClientFactory`. All other HTTP operations go through `IOpencodeApiClient`.
- JSON parsing is done manually (not via `IOpencodeApiClient`'s typed result pattern), so `OpencodeResult<HealthDto>` is not used here.
- Tests require `FakeHttpMessageHandler` infrastructure instead of simple NSubstitute mocks.

## Related Features
server-management-ui

## Related Agents
om-mobile-core (ServerDetailViewModel), om-tester (ServerDetailViewModelTests with FakeHttpMessageHandler)
