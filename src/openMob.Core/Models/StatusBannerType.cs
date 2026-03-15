namespace openMob.Core.Models;

/// <summary>
/// Categorises the type of status banner displayed below the chat header.
/// </summary>
public enum StatusBannerType
{
    /// <summary>No banner should be displayed.</summary>
    None,

    /// <summary>The opencode server is not reachable.</summary>
    ServerOffline,

    /// <summary>No AI provider is configured on the server.</summary>
    NoProvider,

    /// <summary>A tool execution error occurred during the session.</summary>
    ToolError,

    /// <summary>The conversation context is approaching the model's limit.</summary>
    ContextOverflow,
}
