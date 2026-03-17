using System.Text.Json;
using System.Text.Json.Serialization;

namespace openMob.Core.Infrastructure.Http.Dtos.Opencode;

/// <summary>
/// Response DTO for the <c>GET /config</c> and <c>PUT /config</c> endpoints.
/// Maps the top-level scalar fields of the <c>Config</c> TypeScript type faithfully.
/// Complex nested objects are represented as <see cref="JsonElement"/> for v1 flexibility.
/// </summary>
/// <param name="Theme">Theme name to use for the interface.</param>
/// <param name="LogLevel">Log level: DEBUG, INFO, WARN, or ERROR.</param>
/// <param name="Model">Model to use in the format <c>provider/model</c>.</param>
/// <param name="SmallModel">Small model for lightweight tasks such as title generation.</param>
/// <param name="Username">Custom username to display in conversations.</param>
/// <param name="Share">Sharing behaviour: <c>manual</c>, <c>auto</c>, or <c>disabled</c>.</param>
/// <param name="Autoupdate">Whether to automatically update the server.</param>
/// <param name="Snapshot">Whether snapshot mode is enabled.</param>
/// <param name="Keybinds">Raw keybind configuration (complex nested object).</param>
/// <param name="Tui">Raw TUI configuration (complex nested object).</param>
/// <param name="Command">Raw command configuration (complex nested object).</param>
/// <param name="Agent">Raw agent configuration (complex nested object).</param>
/// <param name="Provider">Raw provider configuration (complex nested object).</param>
/// <param name="Mcp">Raw MCP server configuration (complex nested object).</param>
/// <param name="Lsp">Raw LSP configuration (complex nested object).</param>
/// <param name="Formatter">Raw formatter configuration (complex nested object).</param>
/// <param name="Permission">Raw permission configuration (complex nested object).</param>
/// <param name="Tools">Raw tools configuration (complex nested object).</param>
/// <param name="Experimental">Raw experimental features configuration (complex nested object).</param>
/// <param name="Watcher">Raw watcher configuration (complex nested object).</param>
public sealed record ConfigDto(
    [property: JsonPropertyName("theme")] string? Theme,
    [property: JsonPropertyName("logLevel")] string? LogLevel,
    [property: JsonPropertyName("model")] string? Model,
    [property: JsonPropertyName("small_model")] string? SmallModel,
    [property: JsonPropertyName("username")] string? Username,
    [property: JsonPropertyName("share")] string? Share,
    [property: JsonPropertyName("autoupdate")] JsonElement? Autoupdate,
    [property: JsonPropertyName("snapshot")] bool? Snapshot,
    [property: JsonPropertyName("keybinds")] JsonElement? Keybinds,
    [property: JsonPropertyName("tui")] JsonElement? Tui,
    [property: JsonPropertyName("command")] JsonElement? Command,
    [property: JsonPropertyName("agent")] JsonElement? Agent,
    [property: JsonPropertyName("provider")] JsonElement? Provider,
    [property: JsonPropertyName("mcp")] JsonElement? Mcp,
    [property: JsonPropertyName("lsp")] JsonElement? Lsp,
    [property: JsonPropertyName("formatter")] JsonElement? Formatter,
    [property: JsonPropertyName("permission")] JsonElement? Permission,
    [property: JsonPropertyName("tools")] JsonElement? Tools,
    [property: JsonPropertyName("experimental")] JsonElement? Experimental,
    [property: JsonPropertyName("watcher")] JsonElement? Watcher
);

/// <summary>
/// Response DTO for <c>GET /config/providers</c>.
/// Returns only the providers that are configured on the server, with their active models.
/// Each provider entry reuses <see cref="ProviderDto"/> which carries <c>Id</c>, <c>Name</c>,
/// <c>Models</c> (JsonElement), and other fields.
/// </summary>
/// <param name="Providers">
/// An ordered list of configured providers, or <c>null</c> if the server returns an
/// empty or absent providers section.
/// </param>
/// <param name="Default">
/// A map of provider ID → default model ID for each configured provider.
/// May be <c>null</c> if the server omits the field.
/// </param>
public sealed record ConfigProvidersDto(
    [property: JsonPropertyName("providers")]
    IReadOnlyList<ProviderDto>? Providers,

    [property: JsonPropertyName("default")]
    IReadOnlyDictionary<string, string>? Default
);
